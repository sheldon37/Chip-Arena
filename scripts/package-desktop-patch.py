"""Build and verify a runtime patch against supported desktop releases."""
import argparse,hashlib,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--full',required=True);p.add_argument('--base',action='append',required=True);p.add_argument('--out',required=True);a=p.parse_args()
prefix='Chip-Arena-Desktop/';out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
readme='''CHIP ARENA - SELECTIONS AND EASY JOIN UPDATE (0.3.3)

For the existing Windows desktop edition (0.3.0, 0.3.1 or 0.3.2).

INSTALL
1. Close Chip Arena completely.
2. Extract this patch ZIP.
3. Copy its resources folder into your existing Chip-Arena-Desktop folder,
   beside Chip Arena.exe. Merge folders and replace matching files.
   Keep the rest of your existing resources folder: it contains the runtimes.
4. Open Chip Arena.exe. No new .bat is needed.

HOST: Host online > Create Room > Copy Code. Share CA-XXXX-XXXX with friends.
JOIN: Enter that code under Join a friend in the updated desktop launcher.
The code finds the host's current address. Keep the host and room open.
Full invite links still work. Code lookup is separate from gameplay traffic.
Both host and guests should apply this update for launcher code entry.

Chip selection now shatters the other Chips, centers the installed upgrade,
shows the player's name and holds the result before fading it out for everyone.
Create Room / Play vs Bot: set the selection timer to 5-120 seconds per player.
Customize > Intro text: write the line that appears beneath your intro name.

This patch includes the prior connection retries, local-host connection and
combat compression fixes. Lobby / Esc > Connection report remains available.
READ-ME.txt is the full guide. chip-arena-source.zip includes editable source.
Built and checked on Linux. Native Windows display and GPU checks remain local.
'''
bases=[ZipFile(path) for path in a.base]
try:
 with ZipFile(a.full) as full,ZipFile(out,'w',ZIP_DEFLATED,compresslevel=9) as patch:
  selected={}
  for name in full.namelist():
   relative=name.removeprefix(prefix)
   if not (relative.startswith(('resources/app/','resources/game/launcher/','resources/game/dist/client/','resources/game/drizzle/')) or relative in ('resources/game/server.mjs','resources/game/release.json','chip-arena-source.zip','READ-ME.txt')):continue
   data=full.read(name)
   if relative.startswith(('resources/app/','resources/game/launcher/')) or any(name not in base.namelist() or base.read(name)!=data for base in bases):selected[relative]=data;patch.writestr(relative,data)
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
