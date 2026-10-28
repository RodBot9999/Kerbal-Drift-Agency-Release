"""Read selected Automation driveline actuators; never infer hardware from AWD alone."""
from .characteristics import numeric

def enrich_active_drive(m,order,variables):
    hardware={key:{'enabled':False,'max_bias_offset':0} for key in ('center','front','rear')}
    for _,part in order:
        devices={row[1]:row[4] for row in part.get('powertrain',[])[1:] if isinstance(row,list) and len(row)>4 and row[0]=='differential' and isinstance(row[4],dict)}
        for row in part.get('controller',[])[1:]:
            if not isinstance(row,list) or len(row)<2 or not isinstance(row[1],dict):continue
            path=row[0];cfg=row[1];device=cfg.get('differentialName');props=devices.get(device)
            if props is None:continue
            key='center' if path=='drivingDynamics/actuators/camso_advawd' and device=='transferCase' else 'front' if device=='frontDiff' else 'rear' if device=='rearDiff' else None
            enabled=key=='center' and m['drivetrain']=='AWD' or key in ('front','rear') and path=='drivingDynamics/actuators/activeDiffBias' and props.get('diffType')=='torqueVectoring'
            if not enabled:continue
            offset=numeric(cfg.get('maxBiasOffset',.2),variables)
            if not 0<=offset<=1:raise ValueError('Invalid active differential bias offset')
            hardware[key]={'enabled':True,'max_bias_offset':offset,'source_controller':path,'source_device':device}
    m['drive']['active_hardware']=hardware

def config(m):
    hw=m.get('drive',{}).get('active_hardware',{})
    values=[]
    for key,flag,offset in (('center','importedActiveCenter','activeCenterOffset'),('front','importedFrontVectoring','frontVectoringOffset'),('rear','importedRearVectoring','rearVectoringOffset')):
        data=hw.get(key,{});values.append(flag+' = '+str(bool(data.get('enabled',False))).lower());values.append(offset+' = '+str(data.get('max_bias_offset',0)))
    return '\n'.join(values)+'\n'
