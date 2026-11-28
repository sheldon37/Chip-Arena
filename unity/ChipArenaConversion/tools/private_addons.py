"""Explicit private add-on allowlist. Hashes establish integrity, never code trust."""
import json
from pathlib import PurePosixPath

MANIFEST = 'private-addons.json'
ROOT = 'PrivateAddons'
KINDS = {'.cs', '.asmdef', '.shader', '.cginc', '.hlsl', '.compute', '.meta', '.asset', '.mat', '.prefab', '.png', '.bytes'}
BUDGET = 64 * 1024 * 1024


def allowed(rel):
    if not isinstance(rel, str):
        return False
    p = PurePosixPath(rel)
    return (rel == p.as_posix() and not p.is_absolute() and not any(c in rel for c in '\\:*?')
            and '..' not in p.parts and (rel == ROOT + '.meta' or p.parts[0] == ROOT)
            and not any(x.lower().startswith(('demo', 'example', 'sample')) for x in p.parts)
            and p.suffix.lower() in KINDS)


def verify_files(content, files):
    import install_local_assets as assets
    if not isinstance(files, dict) or not files or len(files) > 5000:
        raise assets.Problem('private add-ons: expected a bounded nonempty file allowlist')
    total, folded, guids = 0, set(), set()
    for rel, digest in files.items():
        if not allowed(rel) or rel.casefold() in folded:
            raise assets.Problem('private add-ons: unsafe or duplicate path ' + repr(rel))
        folded.add(rel.casefold())
        path = content / rel
        if any(p.is_symlink() for p in [path, *list(path.parents)[:len(PurePosixPath(rel).parts)-1]]) or not path.is_file():
            raise assets.Problem('private add-ons: missing or linked ' + rel)
        size = path.stat().st_size
        total += size
        if total > BUDGET or size > assets.MAX_FILE_BYTES:
            raise assets.Problem('private add-ons: size budget exceeded')
        if not isinstance(digest, str) or not assets.SHA256.fullmatch(digest) or assets.sha256(path) != digest:
            raise assets.Problem('private add-ons: hash mismatch ' + rel)
        if rel.endswith('.meta'):
            match = assets.GUID.search(path.read_text(encoding='utf-8'))
            if not match or match[1] in guids:
                raise assets.Problem('private add-ons: invalid or duplicate GUID ' + rel)
            guids.add(match[1])
            target = rel[:-5]
            if target not in files and not any(x.startswith(target + '/') for x in files):
                raise assets.Problem('private add-ons: orphan meta ' + rel)
        elif rel + '.meta' not in files:
            raise assets.Problem('private add-ons: missing meta ' + rel)
        for parent in list(PurePosixPath(rel).parents)[:-1]:
            if str(parent) + '.meta' not in files:
                raise assets.Problem('private add-ons: missing folder meta ' + str(parent))
    return files


def verify(content):
    import install_local_assets as assets
    listing = content / MANIFEST
    if not listing.exists():
        return {}
    try:
        data = json.loads(listing.read_text(encoding='utf-8'))
        if not isinstance(data, dict) or data.get('schema') != 1 or set(data) != {'schema', 'files'}:
            raise ValueError('expected schema 1 and files')
        return verify_files(content, data['files'])
    except (ValueError, OSError) as error:
        raise assets.Problem('private add-ons: ' + str(error)) from error


def collect(local, allowlist):
    import install_local_assets as assets
    try:
        data = json.loads(allowlist.read_text(encoding='utf-8'))
        paths = data['files']
        if data.get('schema') != 1 or set(data) != {'schema', 'files'} or not isinstance(paths, list) or not paths:
            raise ValueError('expected schema 1 and explicit files array')
        if any(not allowed(p) for p in paths) or len(set(paths)) != len(paths):
            raise ValueError('unsafe or duplicate path')
        # No globbing or dependency traversal: the owner reviews every file, including metas.
        for rel in paths:
            path = local / rel
            if any(p.is_symlink() for p in [path, *list(path.parents)[:len(PurePosixPath(rel).parts)-1]]):
                raise ValueError('linked path ' + rel)
        files = {p: assets.sha256(local / p) for p in paths}
        return verify_files(local, files)
    except (KeyError, TypeError, ValueError, OSError) as error:
        raise assets.Problem('private add-ons: ' + str(error)) from error
