"""Optional, owner-supplied Magica/bridge bundle. Never fetched or committed by this tool.

Hashes provide integrity, not trust: Unity compiles this explicitly packed local code.
Only the Magica tree (without examples) and the single cloth bridge may be installed.
"""
import json
from pathlib import PurePosixPath

MANIFEST = 'cloth-runtime.json'
BRIDGE = 'ChipArenaLocalTools/Runtime/ChipArenaClothBridge.cs'
BUDGET = 12 * 1024 * 1024
KINDS = {'.cs', '.asmdef', '.asmref', '.meta', '.json', '.txt', '.md', '.png', '.jpg', '.asset', '.mat', '.shader', '.hlsl', '.cginc', '.compute', '.uxml', '.uss'}


def allowed(rel):
    p = PurePosixPath(rel)
    if not rel or p.as_posix() != rel or '\\' in rel or ':' in rel or p.is_absolute() or '..' in p.parts:
        return False
    if any(part.lower().startswith(('example', 'sample')) for part in p.parts):
        return False
    if rel in (BRIDGE, BRIDGE + '.meta', 'ChipArenaLocalTools.meta', 'ChipArenaLocalTools/Runtime.meta', 'MagicaCloth2.meta'):
        return True
    return p.parts[0] == 'MagicaCloth2' and len(p.parts) > 1 and p.suffix.lower() in KINDS


def verify(content):
    import install_local_assets as assets
    listing = content / MANIFEST
    if not listing.exists():
        return {}
    try:
        data = json.loads(listing.read_text(encoding='utf-8'))
    except (OSError, ValueError) as error:
        raise assets.Problem('cloth runtime: ' + str(error))
    if not isinstance(data, dict) or data.get('schema') != 1 or not isinstance(data.get('files'), dict) or set(data) - {'schema', 'files', 'note'}:
        raise assets.Problem('cloth runtime: expected schema 1 and files')
    files = data['files']
    if len(files) > 3000 or BRIDGE not in files or not any(p.startswith('MagicaCloth2/') and p.endswith('.asmdef') for p in files) or not any(p.startswith('MagicaCloth2/') and p.endswith('.cs') for p in files):
        raise assets.Problem('cloth runtime: requires Magica scripts, assembly definition and the bridge')
    total, guids, folded = 0, set(), set()
    for rel, digest in files.items():
        if not allowed(rel) or rel.casefold() in folded:
            raise assets.Problem('cloth runtime: unsafe or duplicate path ' + repr(rel))
        folded.add(rel.casefold())
        path = content / rel
        if any(p.is_symlink() for p in [path, *list(path.parents)[:len(PurePosixPath(rel).parts)-1]]) or not path.is_file():
            raise assets.Problem('cloth runtime: missing or linked ' + rel)
        total += path.stat().st_size
        if total > BUDGET or path.stat().st_size > assets.MAX_FILE_BYTES:
            raise assets.Problem('cloth runtime: size budget exceeded')
        if not isinstance(digest, str) or not assets.SHA256.fullmatch(digest) or assets.sha256(path) != digest:
            raise assets.Problem('cloth runtime: hash mismatch ' + rel)
        if rel.endswith('.meta'):
            match = assets.GUID.search(path.read_text(encoding='utf-8'))
            if not match or match[1] in guids:
                raise assets.Problem('cloth runtime: invalid or duplicate GUID ' + rel)
            guids.add(match[1])
        elif rel + '.meta' not in files:
            raise assets.Problem('cloth runtime: missing meta for ' + rel)
        for parent in list(PurePosixPath(rel).parents)[:-1]:
            if str(parent) + '.meta' not in files:
                raise assets.Problem('cloth runtime: missing folder meta for ' + str(parent))
    return files


def collect(local):
    import install_local_assets as assets
    if not (local / 'MagicaCloth2').is_dir() or not (local / BRIDGE).is_file():
        raise assets.Problem('--with-cloth requires local MagicaCloth2 and the regenerated ChipArenaClothBridge.cs')
    paths = {BRIDGE, BRIDGE + '.meta', 'ChipArenaLocalTools.meta', 'ChipArenaLocalTools/Runtime.meta', 'MagicaCloth2.meta'}
    for path in (local / 'MagicaCloth2').rglob('*'):
        if path.is_symlink():
            raise assets.Problem('cloth runtime: symlink ' + str(path))
        rel = path.relative_to(local).as_posix()
        if path.is_file() and allowed(rel):
            paths.add(rel)
    if any(not (local / rel).is_file() for rel in paths):
        raise assets.Problem('cloth runtime: missing required file or folder meta')
    return {rel: assets.sha256(local / rel) for rel in sorted(paths)}
