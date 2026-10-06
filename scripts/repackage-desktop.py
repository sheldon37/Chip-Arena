"""Reuse a checksum-verified desktop release's runtimes with the current game build."""
import argparse,hashlib,json,struct
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--base',required=True);p.add_argument('--base-sha256',required=True);p.add_argument('--out',required=True);a=p.parse_args()
repo=Path(__file__).resolve().parent.parent;out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
assert sha(Path(a.base).read_bytes())==a.base_sha256,'Base release checksum mismatch'
prefix='Chip-Arena-Desktop/';pins=json.loads((repo/'desktop/runtime-manifest.json').read_text())
def pe64(b):
 assert b[:2]==b'MZ';at=struct.unpack_from('<I',b,0x3c)[0];assert b[at:at+4]==b'PE\0\0';assert struct.unpack_from('<H',b,at+4)[0]==0x8664
with ZipFile(a.base) as base,ZipFile(out,'w',ZIP_DEFLATED,compresslevel=6) as z:
 assert base.testzip() is None
 assert json.loads(base.read(prefix+'resources/app/runtime-manifest.json'))==pins,'Runtime pins changed; package fresh official runtimes instead'
 assert sha(base.read(prefix+'resources/game/runtime/node.exe'))==pins['nodeExecutableSha256']
 assert sha(base.read(prefix+'resources/game/runtime/cloudflared.exe'))=='f096265ec2fcbe9bb6e2d64268db167ced3fcbb83d894bdb9e2fcdb26f2ea7e2'
 for name in base.namelist():
  relative=name.removeprefix(prefix)
  if name.endswith('/') or relative.startswith('resources/app/') or relative.startswith('resources/game/') and not relative.startswith(('resources/game/runtime/','resources/game/THIRD-PARTY-LICENSES/')) or relative in ('chip-arena-source.zip','READ-ME.txt'):continue
  z.writestr(name,base.read(name))
 for f in (repo/'desktop').iterdir():
  if f.is_file():z.writestr(prefix+'resources/app/'+f.name,f.read_bytes())
 for directory in ('dist/client','drizzle','launcher'):
  for f in (repo/directory).rglob('*'):
   if not f.is_file() or 'updates' in f.relative_to(repo).parts:continue
   if directory=='launcher' and f.suffix!='.mjs':continue
   z.writestr(prefix+'resources/game/'+f.relative_to(repo).as_posix(),f.read_bytes())
 z.writestr(prefix+'resources/game/server.mjs',(repo/'.cache/release/server.mjs').read_bytes())
 z.writestr(prefix+'resources/game/release.json',(repo/'.cache/release/release.json').read_bytes())
 z.writestr(prefix+'chip-arena-source.zip',(repo/'dist/client/chip-arena-source.zip').read_bytes())
 z.writestr(prefix+'READ-ME.txt',(repo/'docs/DESKTOP.txt').read_bytes())
with ZipFile(out) as z:
 assert z.testzip() is None;assert len(z.namelist())==len(set(z.namelist()))
 for executable in ('Chip Arena.exe','resources/game/runtime/node.exe','resources/game/runtime/cloudflared.exe'):pe64(z.read(prefix+executable))
 for path in ('resources/app/bundled.cjs','resources/app/lan-socket.cjs','resources/game/dist/client/music/moonlit-wilderness.mp3','resources/game/dist/client/music/violet-avenue.mp3','resources/app/update.cjs','resources/game/launcher/manual-update.mjs','resources/game/dist/client/audio/demon-step.wav','resources/game/dist/client/audio/electric-hook.wav','resources/game/dist/client/audio/intros/dev-emperor.mp3','resources/app/accounts.cjs','resources/app/game-preload.cjs','resources/game/dist/client/audio/arcade-parry.wav','resources/game/dist/client/audio/arcade-punch.wav','resources/game/dist/client/audio/arcade-kick.wav','resources/game/dist/client/audio/arcade-heavy.wav','resources/game/dist/client/audio/intros/dev-ansatsuken.mp3','resources/game/drizzle/0004_concerned_dust.sql','resources/game/dist/client/images/l44t-slenderman.jpg','resources/game/dist/client/audio/intros/dev-matrix.mp3','resources/game/drizzle/0003_classy_tana_nile.sql'):assert prefix+path in z.namelist(),path
 release=json.loads(z.read(prefix+'resources/game/release.json'));assert release['version']==json.loads(next(f for f in (repo/'dist/client/updates/latest.json',repo/'dist/client/updates/latest-v2.json') if f.exists()).read_text())['version']
print(json.dumps({'path':str(out),'bytes':out.stat().st_size,'sha256':sha(out.read_bytes()),'gameVersion':release['version'],'electron':pins['electron'],'architecture':'Windows x64','zipIntegrity':'passed'}))
