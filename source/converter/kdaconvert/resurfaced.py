"""Optional PBR materials. Stock MU materials remain the fallback."""
from pathlib import Path
import numpy as np
from PIL import Image


def bake_surface(output, stem, rgba, roughness, metallic, masked, coat=0):
    rgba=rgba.copy()
    # Resurfaced AlphaTest shares opacity and gloss in MainTex alpha.
    # Keep the exact opacity pixels; its smoothness is necessarily uniform.
    gloss=np.clip(1-roughness,0,1)
    # Standard has no independent clearcoat lobe. Approximate a smooth
    # dielectric coat, retaining some metallic substrate response.
    gloss=np.maximum(gloss,coat*.90)
    effective_metal=np.clip(metallic,0,1)*(1-.65*coat)
    if not masked:rgba[...,3]=gloss
    metal=np.ones_like(rgba);metal[...,:3]=effective_metal[...,None]
    for suffix,pixels in (('_pbr',rgba),('_metal',metal)):
        Image.fromarray(np.clip(pixels*255,0,255).astype('uint8')).save(Path(output)/(stem+suffix+'.png'))
    # Empty atlas space and fully transparent holes must not make the visible
    # surface mirror-smooth. Only pixels surviving the shader cutoff contribute.
    visible=rgba[...,3]>=.45
    smoothness=float(np.mean(gloss[visible])) if np.any(visible) else 0
    return dict(shader='Resurfaced/StandardAlphaTest' if masked else 'Resurfaced/Standard',main=stem+'_pbr',metal=stem+'_metal',smoothness=smoothness if masked else 1,clearcoat_approximation=bool(coat),cutout_alpha_preserved=bool(masked))


def write_definitions(m,dest):
    dest=Path(dest);url='KDAVehicles/Vehicles/'+m['id']+'/Models/'
    definitions=[]
    for i,r in enumerate(m['visual']['material_conversion']):
        p=r.get('pbr')
        if not p:continue
        p['definition']='kda-pbr-'+m['id']+'-'+str(i)
        normal=p.get('normal')
        specific='_AlphaCutoff = 0.45' if p['cutout_alpha_preserved'] else '_MetalAlbedoMultiplier = 1'
        definitions.append('SHABBY_MATERIAL_DEF\n{\nname = '+p['definition']+'\nshader = '+p['shader']+'\nTEXTURE\n{\n_MainTex = '+url+p['main']+'\n_MetalMap = '+url+p['metal']+('\n_BumpMap = '+url+normal if normal else '')+'\n}\nCOLOR\n{\n_Color = #ffffff\n}\nFLOAT\n{\n_Smoothness = '+str(p['smoothness'])+'\n_Metalness = 1\n'+specific+'\n}\n}\n')
    (dest/'pbr-materials.cfg').write_text('\n'.join(definitions),encoding='utf-8')


def replacements(m,model):
    names=m['visual'].get('model_materials',{}).get(model,[])
    return ''.join('SHABBY_MATERIAL_REPLACE\n{\nmaterialDef = '+r['pbr']['definition']+'\ntargetMaterial = '+r['material']+'\n}\n' for r in m['visual']['material_conversion'] if r.get('pbr') and r['material'] in names)
