"""Transform an unpublished desktop ZIP into an isolated updater test fixture."""
import argparse,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('desktop');p.add_argument('feed');p.add_argument('output');p.add_argument('source');a=p.parse_args()
repo=Path(__file__).resolve().parents[2];prefix='Chip-Arena-Desktop/';new='Chip-Arena-TEST-ONLY/'
with ZipFile(a.desktop) as src,ZipFile(a.output,'w',ZIP_DEFLATED,compresslevel=6) as dst:
 for n in src.namelist():
  data=src.read(n);relative=n.removeprefix(prefix)
  if relative=='resources/app/package.json':
   obj=json.loads(data);obj['main']='test-bootstrap.cjs';data=json.dumps(obj).encode()
  elif relative=='resources/game/launcher/manual-update.mjs':data=(repo/'scripts/release/test-manual-update.mjs').read_bytes()
  elif relative=='resources/game/release.json':
   obj=json.loads(data);obj['version']='test-seed';data=json.dumps(obj).encode()
  dst.writestr(new+relative,data)
 dst.writestr(new+'resources/app/test-bootstrap.cjs',(repo/'scripts/release/test-bootstrap.cjs').read_bytes())
 dst.writestr(new+'resources/game/launcher/local-feed.mjs',(repo/'scripts/release/local-feed.mjs').read_bytes())
 for f in Path(a.feed).iterdir():
  if f.is_file():dst.writestr(new+'resources/game/test-feed/'+f.name,f.read_bytes())
 dst.writestr(new+'TEST-ONLY.txt','NOT A PLAYER RELEASE. Extract into a new writable folder. Close the normal game. Settings use TEST-PROFILE in this folder. The update button reads only the bundled test-feed; it cannot publish or fetch the live update feed. Use a separate Windows account for strongest isolation. Do not sign in or host public rooms during offline testing. See WINDOWS-VALIDATION.md.\n')
with ZipFile(a.output) as z:
 assert z.testzip() is None
 assert json.loads(z.read(new+'resources/app/package.json'))['main']=='test-bootstrap.cjs'
 assert json.loads(z.read(new+'resources/game/release.json'))['version']=='test-seed'
 assert z.read(new+'resources/game/launcher/update.mjs')==(Path(a.source)/'launcher/update.mjs').read_bytes()
print('Isolated Windows test ZIP integrity and bootstrap checks passed')
