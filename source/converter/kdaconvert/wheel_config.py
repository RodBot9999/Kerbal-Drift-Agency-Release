"""Generate a KSP wheel part from the KDA runtime's explicit wheel contract.

No external CFG template is loaded. Transform names are the converter's own
mesh rig API; calibration values preserve the tested KDA handling baseline.
"""
from dataclasses import dataclass
import math
import re


@dataclass(frozen=True)
class WheelCalibration:
    mass_tonnes: float = .011
    steering_degrees: float = 40
    front_spring_nmm: float = 168.75
    rear_spring_nmm: float = 150
    front_bar_nmm: float = 48.75
    rear_bar_nmm: float = 33.75
    bump: float = .8385
    rebound: float = 1.3416
    fast_bump: float = .587
    fast_rebound: float = .9224
    rear_toe_degrees: float = .15
    brake_torque: float = 3
    wheel_speed_max: float = 150


CALIBRATION = WheelCalibration()
GENERATOR_VERSION = 1


def _number(value, name, positive=False):
    number = float(value)
    if not math.isfinite(number) or (number <= 0 if positive else number < 0):
        raise ValueError('Invalid wheel '+name)
    return number


def _value(value):
    if isinstance(value, bool):return str(value).lower()
    if isinstance(value, float):
        if not math.isfinite(value):raise ValueError('Non-finite wheel configuration value')
        return repr(value)
    value = str(value)
    if any(c in value for c in '\r\n{}') or '//' in value:
        raise ValueError('Invalid wheel configuration text')
    return value


def _node(name, fields=(), children=(), depth=0):
    indent = '    '*depth
    lines = [indent+name, indent+'{']
    lines.extend(indent+'    '+key+' = '+_value(value) for key,value in fields)
    for child_name,child_fields,child_children in children:
        lines.extend(_node(child_name,child_fields,child_children,depth+1))
    lines.append(indent+'}')
    return lines


def wheel_part(vehicle, wheel, material_nodes=''):
    """Return one complete part; material replacements use the existing backend."""
    vid = vehicle['id'];corner = wheel['corner'];axle = wheel['axle']
    if not re.fullmatch(r'[A-Za-z0-9_-]+',vid):raise ValueError('Invalid wheel vehicle ID')
    if corner not in ('FL','FR','RL','RR') or axle != ('front' if corner[0]=='F' else 'rear'):
        raise ValueError('Invalid wheel corner/axle')
    if vehicle['drivetrain'] not in ('FWD','RWD','AWD'):raise ValueError('Unsupported wheel drivetrain')
    radius = _number(wheel['radius_m'],'radius',True)
    grip = _number(wheel['grip_calibration'],'grip calibration',True)
    weight = _number(wheel['drive_weight'],'drive weight')
    ratio = _number(wheel['drive_ratio'],'drive ratio',True)
    if weight > 1:raise ValueError('Invalid wheel drive weight')
    c = CALIBRATION;front = axle=='front'
    driven = vehicle['drivetrain']=='AWD' or front==(vehicle['drivetrain']=='FWD')
    prefix = 'kda-'+vid.replace('_','-')
    url = 'KDAVehicles/Vehicles/'+vid+'/Models/'+corner
    modules = []
    def module(name,fields=(),children=()):modules.append(('MODULE',[('name',name),*fields],children))
    module('ModuleWheelBase',[
        ('wheelColliderTransformName','wheelCollider'),('wheelTransformName','wheelPivot'),
        ('wheelType','MOTORIZED'),('useNewFrictionModel',True),('autoFriction',False),
        ('autoFrictionAvailable',False),('frictionMultiplier',1),
        ('adherentStart',.4),('frictionAdherent',.95*grip),
        ('peakStart',1.5),('frictionPeak',1.25*grip),
        ('limitStart',4.5),('frictionLimit',.8*grip),
        ('FitWheelColliderToMesh',False),('radius',radius),('center','0,0,0'),
        ('mass',c.mass_tonnes),('groundHeightOffset',0),
        ('TooltipTitle','Car Wheel'),('TooltipPrimaryField','Motorized')])
    module('KDASuspension',[
        ('baseModuleIndex',0),('suspensionTransformName','suspensionPivot'),
        ('suspensionVersion',2),('axleInitialized',True),
        ('springRateNmm',c.front_spring_nmm if front else c.rear_spring_nmm),
        ('antiRollRate',c.front_bar_nmm if front else c.rear_bar_nmm),
        ('bumpDamping',c.bump),('reboundDamping',c.rebound),
        ('fastBumpDamping',c.fast_bump),('fastReboundDamping',c.fast_rebound),
        ('toeIn',0 if front else c.rear_toe_degrees),
        # Native initialization before KDASuspension owns per-step rates/travel.
        ('suspensionDistance',.1),('targetPosition',.5),('maximumLoad',30),
        ('springRatio',48),('damperRatio',1.3),('useDistributedMass',True)])
    module('ModuleWheelSteering',[
        ('baseModuleIndex',0),('caliperTransformName','SteeringPivot'),
        ('autoSteeringAdjust',False),('responseTweakable',10),('steeringEnabled',front),
        ('steeringRange',c.steering_degrees),('angleTweakable',c.steering_degrees),
        ('steeringResponse',2)])
    module('KDAWheelMotor',[
        ('baseModuleIndex',0),('driveWeight',weight),('driveRatio',ratio),('motorEnabled',driven),
        ('wheelSpeedMax',c.wheel_speed_max),('driveResponse',2),('idleDrain',0)],
        # KSP requires a non-empty curve and uses its maximum as a UI/resource
        # denominator. Preserve 2 kNm normalization; the engine sets actual
        # maxDriveTorque in OnDriveUpdate after KSP evaluates this neutral curve.
        [('torqueCurve',[('key','0 2 0 0'),('key','150 2 0 0')],[])])
    module('KDAWheelBrakes',[('baseModuleIndex',0),('maxBrakeTorque',c.brake_torque),('brakeResponse',1)])
    for name in ['KDAMount','KDAGrip','KDAAxle','KDATireSmoke']:module(name)
    module('ModuleTestSubject',[('environments',15),('useStaging',False),('useEvent',True)])
    module('KDAWheelSelection')
    fields=[('name',prefix+'-'+corner),('module','Part'),('author','KDA'),
        ('scale',1),('rescaleFactor',1),('node_stack_top','0,0,0,1,0,0'),
        ('TechRequired','start'),('entryCost',7000),('cost',760),('category','Ground'),
        ('subcategory',0),('title',vehicle['name']+' '+corner+' wheel'),
        ('manufacturer','Kerbal Drift Agency'),
        ('description','Independent KDA wheel with imported tire calibration, axle-linked coilovers and shared vehicle controls.'),
        ('attachRules','1,0,0,0,0'),('mass',c.mass_tonnes),('dragModelType','none'),
        ('maximum_drag',.3),('minimum_drag',.2),('angularDrag',1),
        ('maxTemp',1200),('crashTolerance',100),('breakingForce',120),('breakingTorque',120),
        ('bulkheadProfiles','srf'),('tags','kda car tire wheel suspension rover drift')]
    text='\n'.join(_node('PART',fields,[('MODEL',[('model',url)],[]),*modules]))+'\n'
    if material_nodes:text=text.rsplit('}',1)[0]+material_nodes+'}\n'
    return text
