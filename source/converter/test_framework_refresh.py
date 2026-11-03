"""Car installation must leave the separately installed framework and garage alone."""
import hashlib,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from kdaconvert import installer
from kdaconvert.version import MIN_FRAMEWORK_BUILD

class ModularInstallTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name)
  self.game=self.root/'game';self.package=self.root/'package';self.game.mkdir()
  (self.game/'KSP_x64.exe').write_bytes(b'test')
  self.write(self.package/'GameData/KDAVehicles/Vehicles/car/vehicle.json',json.dumps({'runtime':{'body_part':'body','wheel_parts':{}}}).encode())
  self.write(self.package/'Ships/SPH/car.craft',b'car')
  self.base=self.game/'GameData/KDAVehicles'
  self.write(self.base/'Plugins/KDAVehicles.dll',b'installed-runtime')
  self.write(self.base/'Plugins/KDACrew.dll',b'installed-crew')
  self.write(self.base/'Crew/seat.cfg',b'installed-seat')
  self.stamp(MIN_FRAMEWORK_BUILD)
  self.write(self.game/'GameData/KDAGarageBuilding/Garage/garage.mu',b'custom-garage')
  self.patches=[patch.object(installer,'ksp_running',return_value=False),patch.object(installer,'validate_package',return_value={'vehicle_id':'car','name':'Car'})]
  for p in self.patches:p.start()
 def tearDown(self):
  for p in reversed(self.patches):p.stop()
  self.temp.cleanup()
 def write(self,path,data):path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
 def stamp(self,build):
  runtime=self.base/'Plugins/KDAVehicles.dll'
  self.write(self.base/'framework.json',json.dumps({'abi':'kda-standalone-1','build':build,'runtime_sha256':hashlib.sha256(runtime.read_bytes()).hexdigest()}).encode())
 def install(self):return installer.install(self.package,self.game,log=lambda _:None)
 def test_installs_only_car_and_craft(self):
  before={p:p.read_bytes() for root in (self.base,self.game/'GameData/KDAGarageBuilding') for p in root.rglob('*') if p.is_file()}
  result=self.install()
  self.assertTrue((self.base/'Vehicles/car/vehicle.json').is_file())
  self.assertTrue(Path(result['craft']).is_file())
  self.assertTrue(all(p.read_bytes()==value for p,value in before.items()))
  self.assertEqual(set(result.keys()),{'vehicle','backup','craft','runtime_reused'})
 def test_missing_base_fails_before_backup(self):
  (self.base/'Plugins/KDAVehicles.dll').unlink()
  with self.assertRaisesRegex(ValueError,'base GameData'):self.install()
  self.assertFalse((self.game/'KDAConverterBackups').exists())
 def test_old_or_modified_base_fails(self):
  self.stamp('2026-10-20-kda9')
  with self.assertRaisesRegex(ValueError,'older'):self.install()
  self.stamp(MIN_FRAMEWORK_BUILD)
  (self.base/'Plugins/KDAVehicles.dll').write_bytes(b'changed')
  with self.assertRaisesRegex(ValueError,'modified'):self.install()
 def test_newer_base_is_preserved(self):
  self.stamp('2026-10-27-kda15')
  self.assertTrue(self.install()['runtime_reused'])
  self.assertEqual((self.base/'Plugins/KDAVehicles.dll').read_bytes(),b'installed-runtime')

if __name__=='__main__':unittest.main()
