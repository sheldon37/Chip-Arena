"""Package the official Electron runtime with the same local game and updater as the portable server."""
import argparse, hashlib, json, struct
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--electron',required=True);p.add_argument('--base',required=True);p.add_argument('--tunnel',required=True);p.add_argument('--out',required=True);a=p.parse_args()
repo=Path(__file__).resolve().parent.parent
out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
def sha(b):return hashlib.sha256(b).hexdigest()
electron=Path(a.electron).read_bytes();tunnel=Path(a.tunnel).read_bytes()
assert sha(electron)=='11c395820a5aaa8ebcc0686b476d0ac98a730274ebfbdc8cf5538a7c2815cb5d','Electron 44.4.5 runtime checksum mismatch'
assert sha(tunnel)=='f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2','Cloudflare connector checksum mismatch'
prefix='Chip-Arena-Desktop/'
def pe64(b):
 assert b[:2]==b'MZ';at=struct.unpack_from('<I',b,0x3c)[0];assert b[at:at+4]==b'PE\x00\x00';assert struct.unpack_from('<H',b,at+4)[0]==0x8664
with ZipFile(a.base) as base,ZipFile(a.electron) as runtime,ZipFile(out,'w',ZIP_DEFLATED,compresslevel=6) as z:
 for info in runtime.infolist():
  if info.is_dir() or info.filename.startswith('resources/default_app'):continue
  name='Chip Arena.exe' if info.filename=='electron.exe' else info.filename
  z.writestr(prefix+name,runtime.read(info))
 for f in (repo/'desktop').iterdir():
  if f.is_file():z.writestr(prefix+'resources/app/'+f.name,f.read_bytes())
 root='Chip-Arena-Windows/'
 node=base.read(root+'runtime/node.exe');pe64(node)
 # Node was checksum-verified in the portable release; pin the exact included executable too.
 expected=(repo/'desktop/runtime-manifest.json')
 metadata=json.loads(expected.read_text());assert sha(node)==metadata['nodeExecutableSha256']
 for n in base.namelist():
  relative=n.removeprefix(root)
  if relative.startswith(('runtime/','THIRD-PARTY-LICENSES/')) and not n.endswith('/'):
   z.writestr(prefix+'resources/game/'+relative,base.read(n))
 z.writestr(prefix+'resources/game/runtime/cloudflared.exe',tunnel)
 for directory in ('dist/client','drizzle','launcher'):
  for f in (repo/directory).rglob('*'):
   if not f.is_file() or 'updates' in f.relative_to(repo).parts:continue
   if directory=='launcher' and f.suffix!='.mjs':continue
   z.writestr(prefix+'resources/game/'+str(f.relative_to(repo)),f.read_bytes())
 z.writestr(prefix+'resources/game/server.mjs',Path('/tmp/chip-arena-update-server.mjs').read_bytes())
 z.writestr(prefix+'resources/game/release.json',Path('/tmp/chip-arena-release.json').read_bytes())
 z.writestr(prefix+'chip-arena-source.zip',(repo/'dist/client/chip-arena-source.zip').read_bytes())
 z.writestr(prefix+'READ-ME.txt',(repo/'docs/DESKTOP.txt').read_bytes())
with ZipFile(out) as z:
 assert z.testzip() is None
 pe64(z.read(prefix+'Chip Arena.exe'))
 for name in ('resources/app/main.cjs','resources/app/join.cjs','resources/app/preload.cjs','resources/app/index.html','resources/game/runtime/node.exe','resources/game/runtime/cloudflared.exe','resources/game/dist/client/index.html','resources/game/server.mjs','resources/game/drizzle/0002_wide_shinko_yamashiro.sql','resources/game/drizzle/0003_classy_tana_nile.sql'):assert name and prefix+name in z.namelist(),name
 release=json.loads(z.read(prefix+'resources/game/release.json'))
 assert release['version']==json.loads((repo/'dist/client/updates/latest.json').read_text())['version']
 assert prefix+'resources/game/dist/client/audio/van-scream.wav' not in z.namelist()
print(json.dumps({'path':str(out),'bytes':out.stat().st_size,'sha256':sha(out.read_bytes()),'gameVersion':release['version'],'electron':'44.4.5','architecture':'Windows x64','zipIntegrity':'passed'}))
