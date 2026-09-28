"""Build and verify a runtime patch against supported desktop releases."""
import argparse,hashlib,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--full',required=True);p.add_argument('--base',action='append',required=True);p.add_argument('--out',required=True);a=p.parse_args()
prefix='Chip-Arena-Desktop/';out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
readme='''CHIP ARENA - ACCOUNTS + ANSATSUKEN UPDATE

For the existing Windows desktop edition (0.3.0 through 0.3.3).

1. Close Chip Arena completely.
2. Extract this patch ZIP.
3. Copy its resources folder into your existing Chip-Arena-Desktop folder,
   beside Chip Arena.exe. Merge folders and replace matching files.
   Keep the rest of resources: it contains the runtimes.
4. Open Chip Arena.exe. No new .bat is needed.

The existing updater also installs this game update when the host restarts.
This cumulative patch includes the earlier launcher connection and room-code fixes.

Home > Log in / Register saves your pilot across computers. Keep the recovery code.
Existing DEV credentials still work; log in once to create the saved DEV account.
Home > DEV Menu grants registered players restricted weapons, intros and flags.
After a grant changes, reload Account and rejoin the room.

DEV/granted player: Melee > Ansatsuken. LMB: three-hit combo. RMB: burning Hadoken.
E: Tatsumaki Senpukyaku. 1: Shoryuken. Twelve exclusive arcade Chips include
charge/release Shinku Hadoken and Ten-Hit Rush. Customize > DEV / Rain Warrior intro.
The L33T staff, previous content and easy room-code joining remain included.

Host online > Create Room > Copy Code. Friends enter CA-XXXX-XXXX in Join a friend.
Keep the host open. Settings and saved room-code behavior are unchanged.

READ-ME.txt has the full guide. chip-arena-source.zip includes editable source.
Built and checked on Linux. Native Windows display/GPU playtesting remains local.
'''

bases=[ZipFile(path) for path in a.base]
try:
 with ZipFile(a.full) as full,ZipFile(out,'w',ZIP_DEFLATED,compresslevel=9) as patch:
  selected={}
  for name in full.namelist():
   relative=name.removeprefix(prefix)
   if not (relative.startswith(('resources/app/','resources/game/launcher/','resources/game/dist/client/','resources/game/drizzle/')) or relative in ('resources/game/server.mjs','resources/game/release.json','chip-arena-source.zip','READ-ME.txt')):continue
   data=full.read(name)
   selected[relative]=data;patch.writestr(relative,data)
  patch.writestr('APPLY-FIX.txt',readme)
 with ZipFile(out) as patch,ZipFile(a.full) as full:
  assert patch.testzip() is None
  assert json.loads(patch.read('resources/app/package.json'))['version']=='0.3.3'
  for base in bases:
   for name in full.namelist():
    if name.endswith('/'):continue
    relative=name.removeprefix(prefix)
    merged=patch.read(relative) if relative in selected else base.read(name)
    assert merged==full.read(name),f'Incomplete patch: {name}'
 print(json.dumps({'path':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'patchedFiles':len(selected),'supportedBases':len(bases),'mergedContents':'identical to full download'}))
finally:
 for base in bases:base.close()
