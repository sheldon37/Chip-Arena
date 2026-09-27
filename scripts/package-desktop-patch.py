"""Build and verify a runtime patch against supported desktop releases."""
import argparse,hashlib,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--full',required=True);p.add_argument('--base',action='append',required=True);p.add_argument('--out',required=True);a=p.parse_args()
prefix='Chip-Arena-Desktop/';out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
readme='''CHIP ARENA - LOCAL HOST CONNECTION FIX 0.3.2

For the existing Windows desktop edition (0.3.0 or 0.3.1).

INSTALL
1. Close Chip Arena completely.
2. Extract this patch ZIP.
3. Copy its resources folder into your existing Chip-Arena-Desktop folder,
   beside Chip Arena.exe. Merge folders and replace matching files.
   Do not delete your existing resources folder: it also contains the runtimes.
4. Open Chip Arena.exe and choose Host online. No .bat file is needed.

The host now connects directly to its local Node server. It does not wait for
public DNS to open the game. Friends still use the public invite. In the lobby,
Copy Invite Link includes the public address and room code, not localhost.

To diagnose an invite problem: press Alt, choose Game > Launcher, expand Server
log & connection checks, and click Check connections. It reports your server
process ID, local server health, public link health and raw tunnel output.
If the local server is healthy but the public link fails, the remaining problem
is reaching the public tunnel, not starting the game server. DNS results on a
friend's PC can differ. Keep hosting open while friends connect.

Closing the game or choosing Stop server ends hosting and expires the public
link. The patch preserves runtime files and saved data. chip-arena-source.zip
contains the updated editable source. READ-ME.txt is the full desktop guide.

Validation: automated launcher checks and a real Node server with a local host
and proxied guest passed on Linux. Windows execution still needs local testing.
'''
bases=[ZipFile(path) for path in a.base]
try:
 with ZipFile(a.full) as full,ZipFile(out,'w',ZIP_DEFLATED,compresslevel=9) as patch:
  selected={}
  for name in full.namelist():
   relative=name.removeprefix(prefix)
   if not (relative.startswith(('resources/app/','resources/game/launcher/','resources/game/dist/client/')) or relative in ('resources/game/server.mjs','resources/game/release.json','chip-arena-source.zip','READ-ME.txt')):continue
   data=full.read(name)
   if relative.startswith(('resources/app/','resources/game/launcher/')) or any(name not in base.namelist() or base.read(name)!=data for base in bases):selected[relative]=data;patch.writestr(relative,data)
  patch.writestr('APPLY-FIX.txt',readme)
 with ZipFile(out) as patch,ZipFile(a.full) as full:
  assert patch.testzip() is None
  assert json.loads(patch.read('resources/app/package.json'))['version']=='0.3.2'
  for base in bases:
   for name in full.namelist():
    if name.endswith('/'):continue
    relative=name.removeprefix(prefix)
    merged=patch.read(relative) if relative in selected else base.read(name)
    assert merged==full.read(name),f'Incomplete patch: {name}'
 print(json.dumps({'path':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'patchedFiles':len(selected),'supportedBases':len(bases),'mergedContents':'identical to full download'}))
finally:
 for base in bases:base.close()
