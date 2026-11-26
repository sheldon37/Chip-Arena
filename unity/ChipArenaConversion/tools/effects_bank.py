"""Optional licensed Unity effects, stored under a dedicated LocalAssets directory."""
import json
import os
from pathlib import Path

ROOT = 'ChipArenaFx'
MANIFEST = 'effects-bank.json'
BUDGET = 64_000_000
KINDS = {'.prefab', '.mat', '.asset', '.fbx', '.png', '.jpg', '.jpeg', '.tga', '.bytes', '.mesh'}


def owns(rel):
    return rel in (MANIFEST, ROOT + '.meta') or rel.startswith(ROOT + '/')


def inventory(local):
    import install_local_assets as assets
    folder = local / ROOT
    if folder.is_symlink() or not folder.is_dir() or not (folder / 'Resources').is_dir():
        raise assets.Problem('effects bank requires ChipArenaFx/Resources')
    files, guids, names = {}, set(), set()
    total = 0
    for path in [local / (ROOT + '.meta'), *sorted(folder.rglob('*'))]:
        if path.is_symlink():
            raise assets.Problem('linked effects asset: ' + str(path))
        if path.is_dir():
            continue
        rel = path.relative_to(local).as_posix()
        if not path.is_file() or rel.casefold() in names or any(
                p.rstrip(' .') != p or ':' in p or '\\' in p for p in Path(rel).parts):
            raise assets.Problem('missing, duplicate or invalid effects asset: ' + rel)
        names.add(rel.casefold())
        size = path.stat().st_size
        total += size
        if total > BUDGET or size > assets.MAX_FILE_BYTES:
            raise assets.Problem('effects bank exceeds size budget')
        if path.suffix == '.meta':
            owner = path.with_suffix('')
            if not owner.exists() or (owner.is_file() and owner.suffix.lower() not in KINDS):
                raise assets.Problem('unexpected effects meta: ' + rel)
            match = assets.GUID.search(path.read_text(encoding='utf-8', errors='replace'))
            if not match or match[1] in guids:
                raise assets.Problem('invalid or duplicate effects GUID: ' + rel)
            guids.add(match[1])
        elif path.suffix.lower() not in KINDS:
            raise assets.Problem('unexpected effects asset: ' + rel)
        files[rel] = assets.sha256(path)
    for path in folder.rglob('*'):
        if path.suffix != '.meta' and path.relative_to(local).as_posix() + '.meta' not in files:
            raise assets.Problem('missing effects meta: ' + str(path))
    if not any(rel.startswith(ROOT + '/Resources/') and not rel.endswith('.meta') for rel in files):
        raise assets.Problem('effects bank has no loadable resources')
    return files


def verify(local):
    import install_local_assets as assets
    listing = local / MANIFEST
    if not listing.exists() and not (local / ROOT).exists() and not (local / (ROOT + '.meta')).exists():
        return {}
    try:
        manifest = json.loads(listing.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise assets.Problem('invalid effects manifest: ' + str(error))
    files = inventory(local)
    if manifest != {'schema': 1, 'files': files}:
        raise assets.Problem('effects manifest differs from the bank paths or hashes')
    return files

def install(content, project, files):
    """Reconcile this installer's previous files while preserving local authoring inputs."""
    import install_local_assets as assets
    local = project / 'Assets/LocalAssets'
    # Keep the receipt outside Assets so Unity does not import it into the project.
    receipt = project / 'Library/ChipArena/effects-bank.json'
    previous = json.loads(receipt.read_text(encoding='utf-8')) if receipt.is_file() else {}
    assets.install_visual_body(content, project, files)
    for rel in previous.keys() - files.keys():
        target = local / rel
        if (rel == ROOT + '.meta' or rel.startswith(ROOT + '/')) and target.resolve().is_relative_to(local.resolve()):
            if target.is_file() and not target.is_symlink() and assets.sha256(target) == previous[rel]:
                target.unlink()
    if previous or files:
        receipt.parent.mkdir(parents=True, exist_ok=True)
        staged = receipt.with_suffix('.tmp')
        staged.write_text(json.dumps(files, sort_keys=True), encoding='utf-8')
        os.replace(staged, receipt)
