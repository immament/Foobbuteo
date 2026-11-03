"""Validate a Unity distribution and produce a complete release ZIP and provenance."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import zipfile

MAX_ASSET_BYTES = 2 * 1024**3
SECRET_SUFFIXES = {'.key', '.pem', '.pfx', '.p12', '.ulf', '.keystore', '.jks'}
UNITY_BACKUP_DIRECTORY_SUFFIX = '_BackUpThisFolder_ButDontShipItWithYourGame'


def is_unity_backup_path(relative_path):
    return any(part.endswith(UNITY_BACKUP_DIRECTORY_SUFFIX) for part in relative_path.parts)


def find_distribution(root, target):
    root = Path(root)
    entry = 'index.html' if target == 'webgl' else 'Foobbuteo.exe'
    candidates = [file for file in root.rglob(entry)
                  if target != 'server' or not is_unity_backup_path(file.relative_to(root))]
    if len(candidates) != 1:
        raise ValueError(f'Expected exactly one {target} distribution under {root}.')
    return candidates[0].parent


def package_build(source, output, target, version, sha, run_id, attempt, unity_version, config_template=None):
    source, output = Path(source), Path(output)
    if not re.fullmatch(r'(0|[1-9]\d*)\.\d{2}\.\d{4}\.[1-9]\d*', version):
        raise ValueError('Invalid reserved version.')
    if target not in ('webgl', 'server') or not re.fullmatch(r'[0-9a-f]{40}', sha):
        raise ValueError('Invalid target or source commit.')
    if not source.is_dir() or source.is_symlink():
        raise ValueError('Expected Unity output directory is missing or linked.')
    if source.resolve() == output.resolve() or source.resolve() in output.resolve().parents:
        raise ValueError('Package output must be outside the Unity output directory.')
    files = sorted(file for file in source.rglob('*')
                   if target != 'server' or not is_unity_backup_path(file.relative_to(source)))
    for file in files:
        if file.is_symlink():
            raise ValueError(f'Symbolic links are not permitted: {file.name}')
        if file.name.lower() == 'appsettings.json' or file.suffix.lower() in SECRET_SUFFIXES:
            raise ValueError(f'Secret/configuration file is not permitted: {file.name}')
    names = [file.relative_to(source).as_posix() for file in files if file.is_file()]
    if target == 'webgl':
        valid = 'index.html' in names and any('.wasm' in name for name in names) and any('.data' in name for name in names)
    else:
        valid = 'Foobbuteo.exe' in names and 'UnityPlayer.dll' in names and any(name.startswith('Foobbuteo_Data/Managed/') for name in names)
    if not valid:
        raise ValueError(f'Incomplete {target} distribution.')
    template = Path(config_template) if target == 'server' and config_template else None
    if template is not None and (template.is_symlink() or not template.is_file()):
        raise ValueError('Configuration template must be a regular, unlinked file.')
    output.mkdir(parents=True, exist_ok=True)
    label = 'WebGL' if target == 'webgl' else 'Server'
    archive_path = output / f'Foobbuteo-{label}-{version}.zip'
    with zipfile.ZipFile(archive_path, 'w', zipfile.ZIP_DEFLATED) as archive:
        for name in names:
            if template is not None and name == 'appsettings.tmpl.json':
                continue
            archive.write(source / name, name)
        if template is not None:
            archive.write(template, 'appsettings.tmpl.json')
    if archive_path.stat().st_size >= MAX_ASSET_BYTES:
        raise ValueError('Release ZIP must be smaller than 2 GiB.')
    with archive_path.open('rb') as archive_file:
        digest = hashlib.file_digest(archive_file, 'sha256').hexdigest()
    manifest = {
        'target': target, 'version': version, 'sha': sha, 'runId': str(run_id),
        'attempt': int(attempt), 'unityVersion': unity_version,
        'file': archive_path.name, 'sha256': digest,
    }
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('source', 'output', 'target', 'version', 'sha', 'run-id', 'attempt', 'unity-version'):
        parser.add_argument(f'--{name}', required=True)
    parser.add_argument('--config-template', type=Path)
    args = vars(parser.parse_args())
    args['source'] = find_distribution(args['source'], args['target'])
    package_build(**args)
