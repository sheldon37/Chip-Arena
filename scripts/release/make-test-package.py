"""Transform an unpublished desktop ZIP into an isolated updater test fixture."""
import argparse,hashlib,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('desktop');p.add_argument('feed');p.add_argument('output');p.add_argument('source');p.add_argument('--channel',help='https test channel base URL ending in /; Update and the badge then read it instead of the bundled feed');a=p.parse_args()
if a.channel and not (a.channel.startswith('https://') and a.channel.endswith('/')):p.error('--channel must be an https URL ending in /')
repo=Path(__file__).resolve().parents[2];prefix='Chip-Arena-Desktop/';new='Chip-Arena-TEST-ONLY/'
# Updater v2 (launcher/update.mjs exports startLauncher) reads latest-v2.json first and self-updates its launcher files.
v2=b'startLauncher' in (Path(a.source)/'launcher/update.mjs').read_bytes()
with ZipFile(a.desktop) as src,ZipFile(a.output,'w',ZIP_DEFLATED,compresslevel=6) as dst:
 for n in src.namelist():
  data=src.read(n);relative=n.removeprefix(prefix)
  if relative=='resources/app/package.json':
   obj=json.loads(data);obj['main']='test-remote-bootstrap.cjs' if a.channel else 'test-bootstrap.cjs';data=json.dumps(obj).encode()
  elif relative=='resources/game/launcher/manual-update.mjs':data=(repo/'scripts/release'/('test-remote-update.mjs' if a.channel else 'test-manual-update.mjs')).read_bytes()
  elif relative=='resources/game/release.json':
   obj=json.loads(data);obj['version']='test-seed';data=json.dumps(obj).encode()
  dst.writestr(new+relative,data)
 dst.writestr(new+'resources/app/test-bootstrap.cjs',(repo/'scripts/release/test-bootstrap.cjs').read_bytes())
 dst.writestr(new+'resources/app/test-update-status.cjs',(repo/'scripts/release/test-update-status.cjs').read_bytes())
 dst.writestr(new+'resources/game/launcher/local-feed.mjs',(repo/'scripts/release/local-feed.mjs').read_bytes())
 if a.channel:
  dst.writestr(new+'resources/app/test-remote-bootstrap.cjs',(repo/'scripts/release/test-remote-bootstrap.cjs').read_bytes())
  dst.writestr(new+'resources/app/test-remote-status.cjs',(repo/'scripts/release/test-remote-status.cjs').read_bytes())
  dst.writestr(new+'resources/game/launcher/remote-feed.mjs',(repo/'scripts/release/remote-feed.mjs').read_bytes())
  dst.writestr(new+'resources/game/test-channel.json',json.dumps({'base':a.channel,'schema2':v2}))
 for f in Path(a.feed).iterdir():
  if f.is_file():dst.writestr(new+'resources/game/test-feed/'+f.name,f.read_bytes())
 dst.writestr(new+'TEST-ONLY.txt',('NOT A PLAYER RELEASE. Update and the update badge read ONLY the test channel '+a.channel+' (never the live feed).\n' if a.channel else '')+('Updater v2: this build reads latest-v2.json first (large updates) and updates its own launcher files on the next start.\n' if a.channel and v2 else '')+'NOT A PLAYER RELEASE. Extract into a new writable folder. Close the normal game. Settings use TEST-PROFILE in this folder. The update button reads only the bundled test-feed; it cannot publish or fetch the live update feed. Use a separate Windows account for strongest isolation. Do not sign in or host public rooms during offline testing. See WINDOWS-VALIDATION.md.\n')
with ZipFile(a.output) as z:
 assert z.testzip() is None
 assert json.loads(z.read(new+'resources/app/package.json'))['main']==('test-remote-bootstrap.cjs' if a.channel else 'test-bootstrap.cjs')
 assert json.loads(z.read(new+'resources/game/release.json'))['version']=='test-seed'
 assert z.read(new+'resources/game/launcher/update.mjs')==(Path(a.source)/'launcher/update.mjs').read_bytes()
 if a.channel:assert json.loads(z.read(new+'resources/game/test-channel.json'))=={'base':a.channel,'schema2':v2}
 manifest=Path(a.feed)/'latest-v2.json'
 if a.channel and v2 and manifest.exists():
  # The seed ZIP must already hold the launcher files that schema 2 delivers, or the first update would re-stage them,
  # and schema 2 must never carry the production manual-update.mjs into a test install.
  for f in json.loads(manifest.read_text())['launcher']:
   assert hashlib.sha256(z.read(new+'resources/game/'+f['path'])).hexdigest()==f['sha256'],'seed launcher differs from schema 2: '+f['path']
  assert z.read(new+'resources/game/launcher/manual-update.mjs')==(repo/'scripts/release/test-remote-update.mjs').read_bytes()
print('Isolated Windows test ZIP integrity and bootstrap checks passed')
