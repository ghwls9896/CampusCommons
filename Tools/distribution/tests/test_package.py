import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

MODULE = Path(__file__).parents[1] / 'prepare_runtime.py'

class PackageTests(unittest.TestCase):
    def setUp(self):
        spec = importlib.util.spec_from_file_location('prepare_runtime', MODULE)
        self.mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(self.mod)

    def test_platform_mapping_rejects_unsupported_architecture(self):
        self.assertEqual(self.mod.platform_id('Darwin', 'arm64'), 'macos-arm64')
        self.assertEqual(self.mod.platform_id('Windows', 'AMD64'), 'windows-x64')
        with self.assertRaises(ValueError): self.mod.platform_id('Darwin', 'x86_64')
        with self.assertRaises(ValueError): self.mod.platform_id('Windows', 'ARM64')

    def test_archive_is_relocatable_with_hash_sizes_and_executable_permissions(self):
        with tempfile.TemporaryDirectory(prefix='runtime 한글 ') as temp:
            root = Path(temp) / 'payload'
            for name in ['python/bin/python3.12','app/live.py','model/manifest.json']:
                p = root/name; p.parent.mkdir(parents=True, exist_ok=True); p.write_text('data')
            (root/'python/bin/python3.12').chmod(0o755)
            out = Path(temp)/'output'
            manifest = self.mod.archive_payload(root,out,'macos-arm64')
            self.assertEqual(manifest['unpackedBytes'],12)
            self.assertEqual(manifest['archiveBytes'],(out/'payload.zip').stat().st_size)
            self.assertEqual(manifest['sha256'],self.mod.sha256(out/'payload.zip'))
            self.assertIn('python/bin/python3.12',manifest['executablePaths'])
            with zipfile.ZipFile(out/'payload.zip') as z:
                self.assertEqual(z.read('app/live.py'),b'data')
                self.assertTrue(all(not p.startswith('/') for p in z.namelist()))

    def test_package_refuses_missing_entrypoint(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaises(ValueError): self.mod.archive_payload(Path(temp),Path(temp)/'out','macos-arm64')

    def test_macos_wheels_match_declared_minimum(self):
        self.assertEqual(self.mod.MACOS_MINIMUM, (15, 0))
        with tempfile.TemporaryDirectory() as temp:
            wheel=Path(temp)/'python/lib/python3.12/site-packages/native.dist-info/WHEEL'
            wheel.parent.mkdir(parents=True)
            wheel.write_text('Tag: cp312-cp312-macosx_15_0_arm64\n')
            self.mod.validate_macos_wheels(Path(temp))
            wheel.write_text('Tag: cp312-cp312-macosx_16_0_arm64\n')
            with self.assertRaises(ValueError): self.mod.validate_macos_wheels(Path(temp))

    def test_sanitized_environment_cannot_use_user_python_packages(self):
        env=self.mod.clean_env({'PYTHONPATH':'/wrong','PYTHONHOME':'/wrong','PATH':'/bin'})
        self.assertNotIn('PYTHONPATH',env);self.assertNotIn('PYTHONHOME',env)
        self.assertEqual(env['PYTHONNOUSERSITE'],'1')

if __name__=='__main__':unittest.main()
