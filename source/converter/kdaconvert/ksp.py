from pathlib import Path
import numpy as np
from .resurfaced import write_definitions,replacements
from .wheel_config import wheel_part,GENERATOR_VERSION
from .driveline_metadata import config as driveline_config

def module(name,values=''):return 'MODULE\n{\nname = '+name+'\n'+values+'\n}\n'
def resource(name,amount):return 'RESOURCE\n{\nname = '+name+'\namount = '+str(amount)+'\nmaxAmount = '+str(amount)+'\n}\n'

def write_ksp(m,dest):
    dest=Path(dest);vid=m['id'];prefix='kda-'+vid.replace('_','-');url='KDAVehicles/Vehicles/'+vid;parts=dest/'Parts';parts.mkdir(exist_ok=True)
    e=m['engine'];g=m['gearbox'];driven=[w for w in m['wheels'] if m['drivetrain']=='AWD' or (w['axle']=='rear')==(m['drivetrain']=='RWD')];radius=sum(w['radius_m'] for w in driven)/len(driven)
    induction_setup=dict(NA=5,Turbo=0,Supercharged=4,TwinCharged=6)[e['induction']]
    engine_label=e['layout']+((' '+format(e['displacement_l'],'.2f')+' L') if 'displacement_l' in e else '')+' - '+e['name']
    body='PART\n{\nname = '+prefix+'-body\nmodule = Part\nauthor = KDA / Automation export\nMODEL\n{\nmodel = '+url+'/Models/body\n}\nrescaleFactor = 1\nscale = 1\nTechRequired = start\ncategory = Pods\ncost = 3000\nentryCost = 0\ntitle = '+m['name']+' [KDA]\nmanufacturer = KDA Vehicles\ndescription = Converted Automation car. Shared KDA suspension and driving framework.\nattachRules = 1,0,1,1,0\nmass = '+str((m['mass_kg']-94)/1000)+'\nCrewCapacity = '+str(m.get('crew_capacity',0))+'\nvesselType = Rover\ncrashTolerance = 100\nmaxTemp = 1200\ndragModelType = default\nmaximum_drag = 0.2\nminimum_drag = 0.15\nangularDrag = 2\n'
    # Preserve selected-export mass moments; the complete reference includes
    # the existing wheel masses and full native fuel.
    origin_z=m.get('origin_beamng_m',[0,0,0])[2]
    hubz=sum(-w['position_beamng_m'][2]+origin_z for w in m['wheels'])/4
    from .mass_distribution import ksp_centers
    centers=ksp_centers(m)
    if centers:
        target,center=centers
        body+='CoMOffset = '+', '.join(format(x,'.9g') for x in center)+'\n'
        body+=module('KDAImportedMass','sourceValid = true\nreferenceMassKg = '+str(m['mass_kg'])+'\nreferenceFrontFraction = '+str(m['mass_distribution']['front_mass_fraction'])+'\nreferenceCenter = '+', '.join(format(x,'.9g') for x in target)+'\nbodyCenter = '+', '.join(format(x,'.9g') for x in center))
        m['mass_distribution'].update(center_ksp_m=target,body_center_ksp_m=center)
    else:
        body+='CoMOffset = 0, 0, '+str(hubz-.15)+'\n'
        body+=module('KDAImportedMass','sourceValid = false')
    for w in m['wheels']:
        x,y,z=[-v for v in w['position_beamng_m']];z+=origin_z;body+=f'node_stack_{w["corner"]} = {x}, {y}, {z}, {(-1 if x<0 else 1)}, 0, 0, 1\n'
    body+=module('ModuleCommand','minimumCrew = 0')+resource('ElectricCharge',200)+resource('LiquidFuel',10)
    body+=module('ModuleDataTransmitter','antennaType = INTERNAL\npacketInterval = 1\npacketSize = 2\npacketResourceCost = 12\nrequiredResource = ElectricCharge\nantennaPower = 5000\noptimumRange = 2500\npacketFloor = .1\npacketCeiling = 5')
    m['mass_budget']={'body_dry_kg':m['mass_kg']-94,'wheels_kg':44,'fuel_full_kg':50,'total_full_kg':m['mass_kg']}
    curve='engineTorqueCurve\n{\n'+''.join(f'key = {rpm:g} {nm:.6f}\n' for rpm,nm in e['torque_curve'])+'}\n'
    body+=module('KDAEngine','importedVehicle = true\n'+driveline_config(m)+f'idleRpm = {e["idle_rpm"]}\nrevLimitRpm = {e["limit_rpm"]}\nreferenceRpm = {e["limit_rpm"]}\nmaximumSupportedRpm = {e["limit_rpm"]+2000}\nlaunchRpm = 3500\nimportedEngineName = {engine_label}\nimportedBoostCalibration = {str(e["induction"]!="NA").lower()}\nimportedPeakBoostBar = {e["max_boost_bar"]}\ninductionSetup = {induction_setup}\nmaxBoostBar = {e["max_boost_bar"]}\nboostTorqueGain = 1\nboostFullRpm = {next((r for r,b in e["boost_curve_bar"] if b>=e["max_boost_bar"]*.9 and b>0),1800)}\nspoolSeconds = {e["spool_seconds"]}\nmechanicalBoostMaxBar = {e.get("mechanical_boost_max_bar",0)}\nsuperchargerEngageRpm = {e.get("supercharger_engage_rpm",0)}\nsuperchargerDisengageRpm = {e.get("supercharger_disengage_rpm",0)}\nforwardGears = {len(g["ratios"])}\nforwardRatios = '+','.join(map(str,g['ratios']))+f'\nfinalDrive = {g["final_drive"]}\nreverseRatio = {g["reverse_ratio"]}\ndrivenWheelRadius = {radius}\npreserveImportedRatios = true\ntransmissionSetup = {dict(automatic=0,manual=1,sequential=2)[g["type"]]}\n'+curve+'importedBoostCurve\n{\n'+''.join(f'key = {rpm:g} {b:.6f}\n' for rpm,b in e["boost_curve_bar"])+'}\n')
    a=m['aero'];points={}
    for axle in ('front','rear'):
        ws=[w for w in m['wheels'] if w['axle']==axle];pos=a.get(axle+'_position_beamng_m',[sum(w['position_beamng_m'][i] for w in ws)/2 for i in range(3)])
        points[axle]=','.join(str(-v+(origin_z if i==2 else 0)) for i,v in enumerate(pos))
    body=body.replace('dragModelType = default','dragModelType = none')
    body+=module('KDAAero',f'frontDownforceN = {a["front_downforce_n"]}\nrearDownforceN = {a["rear_downforce_n"]}\nreferenceSpeedKmh = {a["reference_speed_kmh"]}\nreferenceDensity = {a["reference_density_kg_m3"]}\ndragArea = {a["drag_area_m2"]}\nsideDrag = {a["side_drag_multiplier"]}\nverticalDrag = {a["vertical_drag_multiplier"]}\nconstantFrontN = {a["constant_front_n"]}\nconstantRearN = {a["constant_rear_n"]}\nfrontPoint = {points["front"]}\nrearPoint = {points["rear"]}')
    for mod in ['KDABurnout','KDACarControls','KDAController','KDAHud','KDAProtection','KDALighting','KDAInterior']:body+=module(mod)
    for tip in m.get('exhaust_outlets',[]):
        position=-np.array(tip['position_beamng_m'])+np.array(m['origin_beamng_m']);direction=-np.array(tip['direction_beamng'])
        vec=lambda v:','.join(format(float(x),'.8g') for x in v)
        body+=module('KDAExhaust','integratedOutlet = true\noutletPosition = '+vec(position)+'\noutletDirection = '+vec(direction))
    for corner in sorted({s['corner'] for s in m['visual'].get('suspension_hardware',[]) if s.get('animated',True)}):
        origin=np.array(m['origin_beamng_m']);a=m['suspension_visual_anchors'][corner]
        vec=lambda p:','.join(format(float(x),'.8g') for x in (-np.array(p)+origin))
        hub=next(w['position_beamng_m'] for w in m['wheels'] if w['corner']==corner)
        body+=module('KDASuspensionVisual','visualRoot = KDASuspension_'+corner+'\nhubReference = '+vec(hub)+'\nfixedPoints = '+';'.join(vec(p) for p in a['fixed'])+'\nmovingPoints = '+';'.join(vec(p) for p in a['moving']))
    # Select 8 RPM levels from each original sound bank, mono 22050 Hz.
    sound=dest/'Sounds';sound.mkdir(exist_ok=True);sourceRoot=dest.parents[3] if False else None
    m['runtime']={'body_part':prefix+'-body','wheel_parts':{w['corner']:prefix+'-'+w['corner'] for w in m['wheels']},'shared_plugin':'KDAVehicles/Plugins/KDAVehicles.dll','shared_suspension':True}
    write_definitions(m,dest);body+=replacements(m,'body')
    body+='}\n';(parts/'body.cfg').write_text(body,encoding='utf-8')
    m['runtime']['wheel_config_generator']='kda.independent'
    m['runtime']['wheel_config_version']=GENERATOR_VERSION
    for w in m['wheels']:
        (parts/(w['corner']+'.cfg')).write_text(wheel_part(m,w,replacements(m,w['corner'])),encoding='utf-8')
    return prefix

def write_audio(m,vehicle,dest):
    from .hybrid_audio import write
    write(m,vehicle,dest)

def craft(m,dest):
    prefix='kda-'+m['id'].replace('_','-');text=f'ship = {m["name"]} KDA\nversion = 1.12.5\ntype = SPH\nsize = 2,2,5\n'
    for i,w in enumerate([None]+m['wheels']):
        name=prefix+('-body' if i==0 else '-'+w['corner']);pid=100+i
        if i==0:pos=(0,2,0);rot=(.70710678,0,0,.70710678);att=(0,0,0);attrot=rot
        else:
            x,y,z=[-v for v in w['position_beamng_m']];z+=m.get('origin_beamng_m',[0,0,0])[2];pos=(x,2-z,y);left=x<0;rot=(0,1,0,0) if left else (0,0,0,1);att=(x,y,z);attrot=(0,.70710678,-.70710678,0) if left else (-.70710678,0,0,.70710678)
        vec=lambda v:','.join(str(a) for a in v)
        text+=f'PART\n{{\npart = {name}_{pid}\npartName = Part\npersistentId = {pid}\npos = {vec(pos)}\nrot = {vec(rot)}\nattRot = {vec(attrot)}\nattPos = {vec(att)}\nattPos0 = {vec(att)}\nattRot0 = {vec(attrot)}\nmirror = 1,1,1\nsymMethod = Mirror\nattachMode = 0\ninverseStage = 0\nparent = {(-1 if i==0 else 0)}\nmass = {((m["mass_kg"]-94)/1000 if i==0 else .011)}\n'
        if i==0:
            for j,child in enumerate(m['wheels'],1):text+=f'link = {prefix}-{child["corner"]}_{100+j}\nattN = {child["corner"]},{prefix}-{child["corner"]}_{100+j}\n'
        else:text+=f'attN = top,{prefix}-body_100\n'
        text+='}\n'
    dest.parent.mkdir(parents=True,exist_ok=True);dest.write_text(text,encoding='utf-8')


