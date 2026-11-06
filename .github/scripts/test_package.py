import importlib.util
import json
from pathlib import Path
import tempfile
import subprocess
import sys
import unittest
import zipfile

spec = importlib.util.spec_from_file_location('package_build', Path(__file__).with_name('package.py'))
package = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package)


class PackageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.source = self.root / 'build'
        self.source.mkdir()
        self.output = self.root / 'output'

    def write(self, name, content='build content'):
        path = self.source / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)

    def package(self, target='webgl'):
        return package.package_build(self.source, self.output, target, '0.26.1103.1', 'a' * 40, '123', 1, '6000.6.4f1')

    def web(self):
        for name in ['index.html', 'Build/game.loader.js', 'Build/game.wasm.br', 'Build/game.data.br']:
            self.write(name)

    def test_webgl_archive_preserves_compressed_payload_and_provenance(self):
        self.web()
        manifest = self.package()
        with zipfile.ZipFile(self.output / manifest['file']) as archive:
            self.assertIn('Build/game.wasm.br', archive.namelist())
            self.assertIn('index.html', archive.namelist())
        self.assertEqual(manifest['version'], '0.26.1103.1')
        self.assertEqual(manifest['runId'], '123')
        self.assertEqual(len(manifest['sha256']), 64)

    def test_server_archive_contains_runtime_files_and_excludes_unity_backups(self):
        for name in ['Foobbuteo.exe', 'Foobbuteo_Data/Managed/Assembly-CSharp.dll', 'UnityPlayer.dll']:
            self.write(name)
        backup_file = 'Foobbuteo_BackUpThisFolder_ButDontShipItWithYourGame/Managed/backup.dll'
        self.write(backup_file)
        self.write('nested/Other_BackUpThisFolder_ButDontShipItWithYourGame/appsettings.json')
        manifest = self.package('server')
        with zipfile.ZipFile(self.output / manifest['file']) as archive:
            self.assertEqual(set(archive.namelist()), {
                'Foobbuteo.exe', 'Foobbuteo_Data/Managed/Assembly-CSharp.dll', 'UnityPlayer.dll',
            }, 'Only runtime files should ship; Unity backup folders and their contents must be excluded.')
        self.assertTrue((self.source / backup_file).is_file(), 'Packaging must leave the original backup files intact.')

    def test_server_discovery_ignores_executables_inside_unity_backup_directories(self):
        self.write('StandaloneWindows64/Foobbuteo.exe')
        self.write('StandaloneWindows64/Foobbuteo_BackUpThisFolder_ButDontShipItWithYourGame/Foobbuteo.exe')
        self.assertEqual(package.find_distribution(self.source, 'server'), self.source / 'StandaloneWindows64')

    def test_android_archive_contains_apk_and_preserves_provenance(self):
        self.write('Foobbuteo.apk')
        manifest = self.package('android')
        self.assertEqual(manifest['file'], 'Foobbuteo-Android-0.26.1103.1.zip')
        self.assertEqual(manifest['target'], 'android')
        with zipfile.ZipFile(self.output / manifest['file']) as archive:
            self.assertEqual(archive.read('Foobbuteo.apk'), b'build content')

    def test_android_discovery_requires_exactly_one_apk_distribution(self):
        with self.assertRaises(ValueError):
            package.find_distribution(self.source, 'android')
        self.write('Android/Foobbuteo.apk')
        self.assertEqual(package.find_distribution(self.source, 'android'), self.source / 'Android')
        self.write('other/Foobbuteo.apk')
        with self.assertRaises(ValueError):
            package.find_distribution(self.source, 'android')

    def test_android_rejects_missing_apk_and_loose_keystores(self):
        with self.assertRaises(ValueError):
            self.package('android')
        self.write('Foobbuteo.apk')
        self.write('release.keystore')
        with self.assertRaises(ValueError):
            self.package('android')

    def test_missing_output_or_secret_configuration_is_rejected(self):
        with self.assertRaises(ValueError):
            self.package()
        for prohibited in ['appsettings.json', 'nested/token.ulf', 'private.key']:
            with self.subTest(prohibited=prohibited):
                self.web()
                self.write(prohibited)
                with self.assertRaises(ValueError):
                    self.package()
                (self.source / prohibited).unlink()

    def test_symbolic_links_are_rejected(self):
        self.web()
        try:
            (self.source / 'linked.txt').symlink_to(self.source / 'index.html')
        except OSError:
            self.skipTest('Creating symbolic links is unavailable on this host.')
        with self.assertRaises(ValueError):
            self.package()

    def test_gameci_nested_output_is_located_without_guessing_its_layout(self):
        self.write('WebGL/Foobbuteo/index.html')
        self.assertEqual(package.find_distribution(self.source, 'webgl'), self.source / 'WebGL/Foobbuteo')
        self.write('other/index.html')
        with self.assertRaises(ValueError):
            package.find_distribution(self.source, 'webgl')

    def test_cli_packages_server_template_without_modifying_build_directory(self):
        for name in ['StandaloneWindows64/Foobbuteo.exe', 'StandaloneWindows64/UnityPlayer.dll',
                     'StandaloneWindows64/Foobbuteo_Data/Managed/Assembly-CSharp.dll']:
            self.write(name)
        template = self.root / 'appsettings.tmpl.json'
        template.write_text('{}')
        original_files = {
            file.relative_to(self.source): file.read_bytes()
            for file in self.source.rglob('*') if file.is_file()
        }
        if sys.platform != 'win32':
            directories = [self.source, *(file for file in self.source.rglob('*') if file.is_dir())]

            def restore_permissions():
                for directory in directories:
                    directory.chmod(0o755)

            self.addCleanup(restore_permissions)
            for directory in directories:
                directory.chmod(0o555)
        result = subprocess.run([
            sys.executable, str(Path(package.__file__)), '--source', str(self.source),
            '--output', str(self.output), '--target', 'server', '--version', '0.26.1103.1',
            '--sha', 'a' * 40, '--run-id', '123', '--attempt', '2', '--unity-version', '6000.6.4f1',
            '--config-template', str(template),
        ], capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(
            original_files,
            {file.relative_to(self.source): file.read_bytes()
             for file in self.source.rglob('*') if file.is_file()},
            'Packaging must only read the GameCI build directory, including when adding the deployment template.',
        )
        manifest = json.loads((self.output / 'manifest.json').read_text())
        self.assertEqual(manifest['attempt'], 2)
        with zipfile.ZipFile(self.output / manifest['file']) as archive:
            self.assertIn('appsettings.tmpl.json', archive.namelist())
            self.assertEqual(archive.read('appsettings.tmpl.json'), b'{}')
            self.assertNotIn('appsettings.json', archive.namelist())


if __name__ == '__main__':
    unittest.main()
