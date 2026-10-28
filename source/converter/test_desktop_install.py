"""Install a real converted package into a disposable game when a fixture is supplied."""
import hashlib,json,os,shutil,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from kdaconvert import installer
from kdaconvert.validation import validate_package

ROOT=Path(__file__).resolve().parents[2]
PACKAGE=Path(os.environ.get('KDA_TEST_PACKAGE',ROOT/'source/.build/modular-smoke-package'))

class InstallTests(unittest.TestCase):
 def setUp(self):
  if not PACKAGE.is_dir():self.skipTest('Set KDA_TEST_PACKAGE to a car-only conversion package')
  self.temp=tempfile.TemporaryDirectory(dir=ROOT/'source/.build');self.game=Path(self.temp.name)/'KSP';self.game.mkdir()
  (self.game/'KSP_x64.exe').write_bytes(b'test')
  self.package=Path(self.temp.name)/'package';shutil.copytree(PACKAGE,self.package)
  self.vid=validate_package(self.package)['vehicle_id']
  self.running=patch.object(installer,'ksp_running',return_value=False);self.running.start()
 def tearDown(self):
  if hasattr(self,'running'):self.running.stop();self.temp.cleanup()
 def install_base(self):
  for item in ('Plugins/KDAVehicles.dll','Plugins/KDACrew.dll','Crew/seat.cfg','framework.json'):
   source=ROOT/'GameData/KDAVehicles'/item;target=self.game/'GameData/KDAVehicles'/item
   target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
  shutil.copytree(ROOT/'GameData/KDAVehicles/Sounds',self.game/'GameData/KDAVehicles/Sounds')
 def install(self):return installer.install(self.package,self.game,log=lambda _:None)
 def test_missing_base_rejected_without_changes(self):
  with self.assertRaisesRegex(ValueError,'base GameData'):self.install()
  self.assertFalse((self.game/'KDAConverterBackups').exists())
 def test_install_only_car_and_craft_with_backup(self):
  self.install_base();runtime=self.game/'GameData/KDAVehicles/Plugins/KDAVehicles.dll';before=hashlib.sha256(runtime.read_bytes()).hexdigest()
  result=self.install();target=self.game/'GameData/KDAVehicles/Vehicles'/self.vid
  self.assertTrue((target/'vehicle.json').exists());self.assertTrue(Path(result['craft']).exists())
  self.assertFalse((self.game/'GameData/KDAGarageBuilding').exists())
  self.assertEqual(hashlib.sha256(runtime.read_bytes()).hexdigest(),before)
  (target/'personal-note.txt').write_text('keep me');again=self.install()
  self.assertEqual((Path(again['backup'])/'previous/GameData/KDAVehicles/Vehicles'/self.vid/'personal-note.txt').read_text(),'keep me')
 def test_modified_base_rejected(self):
  self.install_base();runtime=self.game/'GameData/KDAVehicles/Plugins/KDAVehicles.dll';runtime.write_bytes(b'modified')
  with self.assertRaisesRegex(ValueError,'modified'):self.install()
  self.assertFalse((self.game/'GameData/KDAVehicles/Vehicles').exists())
 def test_missing_texture_rejected(self):
  model=next((self.package/'GameData/KDAVehicles/Vehicles'/self.vid/'Models').glob('*.png'))
  model.unlink()
  with self.assertRaisesRegex(ValueError,'texture'):validate_package(self.package)
 def test_missing_hybrid_bank_rejected_before_backup(self):
  self.install_base()
  manifest=json.loads((self.package/'GameData/KDAVehicles/Vehicles'/self.vid/'vehicle.json').read_text())
  if manifest['audio'].get('type')!='kda.hybrid':self.skipTest('Requires a v2 hybrid package')
  sound=self.game/'GameData/KDAVehicles/Sounds/Hybrid'/manifest['audio']['family']/'rpm_0_0.wav'
  sound.unlink()
  with self.assertRaisesRegex(ValueError,'shared hybrid audio bank'):self.install()
  self.assertFalse((self.game/'KDAConverterBackups').exists())

if __name__=='__main__':unittest.main()
