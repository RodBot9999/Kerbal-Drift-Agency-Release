import unittest,tempfile
from pathlib import Path
import numpy as np
from PIL import Image
from kdaconvert.resurfaced import bake_surface

class PbrTests(unittest.TestCase):
    def test_empty_atlas_does_not_raise_masked_smoothness(self):
        with tempfile.TemporaryDirectory() as tmp:
            a=np.zeros((32,32,4),dtype=np.float32);a[8:16,8:16]=1
            rough=np.zeros((32,32));rough[8:16,8:16]=.6
            p=bake_surface(tmp,'grid',a,rough,np.ones((32,32)),True)
            self.assertAlmostEqual(p['smoothness'],.4)
    def test_cutouts_exact_and_gloss_separate_scalar(self):
        with tempfile.TemporaryDirectory() as tmp:
            a=np.ones((4,4,4),dtype=np.float32);a[:2,:,3]=0
            p=bake_surface(tmp,'paint',a,np.full((4,4),.3),np.full((4,4),.75),True,1)
            im=np.asarray(Image.open(Path(tmp)/'paint_pbr.png'))
            np.testing.assert_array_equal(im[:,:,3],(a[:,:,3]*255).astype('uint8'))
            self.assertEqual(p['shader'],'Resurfaced/StandardAlphaTest')
            self.assertAlmostEqual(p['smoothness'],.9)
            self.assertTrue(p['clearcoat_approximation'])
    def test_opaque_roughness_becomes_spatial_gloss(self):
        with tempfile.TemporaryDirectory() as tmp:
            a=np.ones((2,2,4),dtype=np.float32);rough=np.array([[0,1],[.25,.75]])
            p=bake_surface(tmp,'trim',a,rough,np.zeros((2,2)),False)
            im=np.asarray(Image.open(Path(tmp)/'trim_pbr.png'))
            np.testing.assert_array_equal(im[:,:,3],((1-rough)*255).astype('uint8'))
            self.assertEqual(p['shader'],'Resurfaced/Standard')
            self.assertEqual(p['smoothness'],1)

if __name__=='__main__':unittest.main()
