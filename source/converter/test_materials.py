"""Regression checks for the visual data discarded by the original adapter."""
import unittest,tempfile,json
from pathlib import Path
import numpy as np
from PIL import Image
from kdaconvert.materials import reproject,Materials
from kdaconvert.mesh import mu,beam_rotation,wheel_rotation,geometries

class MaterialTests(unittest.TestCase):
    def test_automation_duplicate_uv_labels_keep_both_sources(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            template='''<COLLADA xmlns="http://www.collada.org/2005/11/COLLADASchema"><asset><unit meter="1"/><up_axis>Z_UP</up_axis></asset><library_geometries><geometry id="body-lib"><mesh>
            <source id="p"><float_array>0 0 0 1 0 0 0 1 0</float_array><technique_common><accessor stride="3"/></technique_common></source>
            <source id="body-UV0"><float_array>0 0 8 0 0 8</float_array><technique_common><accessor stride="2"/></technique_common></source>
            <source id="body-UV1"><float_array>.1 .1 .2 .1 .1 .2</float_array><technique_common><accessor stride="2"/></technique_common></source>
            <vertices id="v"><input semantic="POSITION" source="#p"/></vertices><triangles material="grid"><input semantic="VERTEX" offset="0" source="#v"/><input semantic="TEXCOORD" offset="1" set="0" source="#body-UV0"/><input semantic="TEXCOORD" offset="2" set="{second}" source="#body-UV1"/><p>0 0 0 1 1 1 2 2 2</p></triangles></mesh></geometry></library_geometries></COLLADA>'''
            for second in (0,1):
                (root/'test.dae').write_text(template.replace('{second}',str(second)))
                g=geometries(root)['body'][0]
                np.testing.assert_allclose(g['uv0'],[[0,0],[8,0],[0,8]])
                np.testing.assert_allclose(g['uv1'],[[.1,.1],[.2,.1],[.1,.2]])

    def test_grille_holes_keep_stamp_and_fine_tiling(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,1,1,1]}}}))
            grid=np.zeros((16,16,4),dtype=np.uint8);grid[...,3]=255
            grid[:3,:,:3]=255;grid[:,:3,:3]=255
            Image.fromarray(grid).save(root/'camso_t_grille02_h_stepped.dds')
            stamp=Image.new('RGBA',(32,32),(255,255,255,255))
            for y in range(16,32):
                for x in range(32):stamp.putpixel((x,y),(0,0,0,255))
            stamp.save(root/'stamp.dds')
            stage={'diffuseMap':'camso_t_grille02_h_stepped.dds','opacityMap':'stamp.dds','opacityMapUseUV':1}
            (root/'test.materials.json').write_text(json.dumps({'fixturegrille':{'Stages':[stage]},'black_paint':{'Stages':[{'diffuseColor':[0,0,0,0]}]}}))
            uv=np.array([[0,0],[4,0],[0,4],[4,0],[4,4],[0,4]],dtype=float)
            group={'material':'fixturegrille','uv0':uv,'uv1':uv*np.array([.02,.2])+[.1,.1]}
            mats=Materials(root,out,'lightweight',mu,{'body':[group]});index,target=mats.get('fixturegrille')
            self.assertEqual(target,2);self.assertTrue(mats.report[0]['dedicated_grille_atlas'])
            a=np.asarray(Image.open(out/'material_0.png'))[...,3]
            self.assertGreater(np.mean(a[40:220,20:490]>.45*255),.1)
            self.assertLess(np.mean(a[40:220,20:490]>.45*255),.6)
            self.assertEqual(int(a[300:490,20:490].max()),0)
            np.testing.assert_array_equal(a,np.asarray(Image.open(out/'material_0_pbr.png'))[...,3])
            self.assertEqual(mats.materials[index].shaderName,'KSP/Alpha/Cutoff Bumped')
            mats.get('black_paint')
            self.assertEqual(Image.open(out/'material_1.png').getpixel((0,0))[3],255)
    def test_wheel_alignment_and_side_mirroring(self):
        for angles,axis in [([.146,-3,-.0064],[1,0,0]),([-.146,3,179.994],[-1,0,0]),([.146,-1,-.00257],[1,0,0])]:
            np.testing.assert_allclose(wheel_rotation(angles)@np.array([1,0,0]),axis,atol=1e-12)
        p=np.array([.1,.2,.3]);q=wheel_rotation([25,-3,0])@p
        self.assertAlmostEqual(np.linalg.norm(q[1:]),np.linalg.norm(p[1:]))
    def test_glass_texture_alpha(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,1,1,1]}}}))
            Image.new('RGBA',(2,2),(128,128,128,128)).save(root/'glass.dds')
            (root/'test.materials.json').write_text(json.dumps({'glass':{'translucent':True,'Stages':[{'diffuseMap':'glass.dds','diffuseColor':[1,1,1,.5]}]}}))
            mats=Materials(root,out,'balanced',mu,{});mats.get('glass')
            self.assertEqual(Image.open(out/'material_0.png').getpixel((0,0))[3],64)
    def test_ao_and_texture_deduplication(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,1,1,1]}}}))
            Image.new('RGBA',(2,2),(128,128,128,255)).save(root/'ao.dds')
            desc={'Stages':[{'diffuseColor':[.8,.4,.2,1],'ambientOcclusionMap':'ao.dds'}]}
            (root/'test.materials.json').write_text(json.dumps({'a':desc,'b':desc}))
            mats=Materials(root,out,'balanced',mu,{});a,_=mats.get('a');b,_=mats.get('b')
            self.assertEqual(len(mats.textures),1)
            self.assertEqual(mats.materials[a].textureProperties['_MainTex'].index,mats.materials[b].textureProperties['_MainTex'].index)
            pixel=Image.open(out/'material_0.png').getpixel((0,0));self.assertEqual(pixel,(102,51,25,255))
            self.assertTrue(mats.report[0]['ambient_occlusion_baked'])
    def test_spatial_palette(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,0,0,1]},'Default2':{'baseColor':[0,1,0,1]},'Default3':{'baseColor':[0,0,1,1]}}}))
            palette=Image.new('RGBA',(4,4),(255,0,0,255))
            for y in range(4):
                for x in range(2,4):palette.putpixel((x,y),(0,255,0,255))
            palette.save(root/'palette.dds')
            (root/'test.materials.json').write_text(json.dumps({'paint':{'Stages':[{'instanceDiffuse':True,'colorPaletteMap':'palette.dds'}]}}))
            mats=Materials(root,out,'balanced',mu,{})
            mats.get('paint');arr=np.array(Image.open(out/'material_0.png'))
            np.testing.assert_equal(arr[1,0],[255,0,0,255]);np.testing.assert_equal(arr[1,3],[0,255,0,255])
            self.assertTrue(mats.report[0]['spatial_palette_baked'])

    def test_normal_uv_reprojection(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,1,1,1]}}}))
            normal=Image.new('RGBA',(8,8),(128,128,255,255));normal.putpixel((3,3),(160,128,250,255));normal.save(root/'normal.dds')
            Image.new('RGBA',(8,8),(255,255,255,255)).save(root/'mask.dds')
            (root/'test.materials.json').write_text(json.dumps({'paint':{'Stages':[{'normalMap':'normal.dds','normalMapUseUV':0,'opacityMap':'mask.dds','opacityMapUseUV':1}]}}))
            group={'material':'paint','uv0':np.array([[0,0],[1,0],[0,1]]),'uv1':np.array([[.1,.1],[.9,.1],[.1,.9]])}
            mats=Materials(root,out,'balanced',mu,{'body':[group]});mats.get('paint')
            self.assertTrue(mats.report[0]['normal_map_imported']);self.assertFalse(mats.report[0]['normal_map_pending'])
    def test_beamng_zxy_rotation(self):
        np.testing.assert_allclose(beam_rotation([0,90,90])@np.array([1,0,0]),[0,1,0],atol=1e-12)
    def test_uv_reprojection(self):
        image=np.ones((8,8,4),dtype=np.float32);image[:,:,:3]=[.8,.2,.1]
        src=np.array([[0,0],[1,0],[0,1]],dtype=float)
        dst=np.array([[.1,.1],[.9,.1],[.1,.9]],dtype=float)
        baked=reproject(image,src,dst,32)
        np.testing.assert_allclose(baked[20,10],[.8,.2,.1,1],atol=1e-6)
        np.testing.assert_equal(baked[0,31],[0,0,0,0])

    def test_palette_mask_and_opaque_zero_alpha(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);out=root/'Models';out.mkdir()
            (root/'info.json').write_text(json.dumps({'paints':{'Default1':{'baseColor':[1,0,0,1]},'Default2':{'baseColor':[0,1,0,1]}}}))
            Image.new('RGBA',(4,4),(0,255,0,255)).save(root/'palette.dds')
            mask=Image.new('RGBA',(4,4),(255,255,255,255));mask.putpixel((0,0),(0,0,0,255));mask.save(root/'mask.dds')
            (root/'test.materials.json').write_text(json.dumps({'paint':{'Stages':[{'instanceDiffuse':True,'colorPaletteMap':'palette.dds','opacityMap':'mask.dds','opacityMapUseUV':1}]},'rubber':{'Stages':[{'diffuseColor':[.02,.02,.02,0]}]}}))
            mats=Materials(root,out,'balanced',mu,{})
            index,uv=mats.get('paint');self.assertEqual(uv,1)
            self.assertEqual(mats.materials[index].shaderName,'KSP/Alpha/Cutoff Bumped')
            self.assertNotIn('_Shininess',mats.materials[index].floatProperties2)
            arr=np.array(Image.open(out/'material_0.png'))
            np.testing.assert_equal(arr[1,1],[0,255,0,255]);self.assertEqual(arr[0,0,3],0)
            mats.get('rubber');self.assertEqual(Image.open(out/'material_1.png').getpixel((0,0))[3],255)

if __name__=='__main__':unittest.main()
