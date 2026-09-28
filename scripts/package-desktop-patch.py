"""Build and verify a runtime patch against supported desktop releases."""
import argparse,hashlib,json
from pathlib import Path
from zipfile import ZipFile,ZIP_DEFLATED
p=argparse.ArgumentParser();p.add_argument('--full',required=True);p.add_argument('--base',action='append',required=True);p.add_argument('--out',required=True);a=p.parse_args()
prefix='Chip-Arena-Desktop/';out=Path(a.out).resolve();out.parent.mkdir(parents=True,exist_ok=True)
readme='''CHIP ARENA - MAPS, BOTS & DIRECT START (DESKTOP 0.3.6)

For the existing Windows desktop edition. Verified against desktop 0.3.5.

1. Close Chip Arena completely.
2. Extract this patch ZIP.
3. Copy its resources folder into your existing Chip-Arena-Desktop folder,
   beside Chip Arena.exe. Merge folders and replace matching files.
   Keep the rest of resources: it contains the runtimes.
4. Open Chip Arena.exe. No new .bat or separate server launch is needed.

The game now opens directly to its main menu. Play vs Bots runs locally;
Create Room starts the authoritative server. Online rooms also start the
public connector. Turn Online room off for LAN-only hosting.
Share Copy Code with friends; they enter it in Join Room. Keep the host open.
For LAN rooms, share Copy Invite Link with friends on the same network.

Update Game is on the main menu and in Settings. Apply this desktop patch
once to get the new startup flow; afterward the button installs game-content
updates. Desktop shell/runtime upgrades still require a patch or full ZIP.
Your saved account login is retained. Account customization syncs after login.

Ten new maps join Shatter Foundry, with buildings, varied cover and scenery.
Moonlit Wilderness has a flat dandelion field, a huge moon and a castle.
It uses your supplied full Moonlite Wilderness track. The other new maps
have nine new original electronic tracks. Music volume and mute still apply.

Choose 1-7 bots in Play vs Bots, or 0-7 bots when creating a room, subject to
the eight-pilot limit. The host can change map, bot count and difficulty in
the lobby. Changing settings clears human Ready states. Bots ready themselves.

Emperor Electric Hook adds a short forward step, much shorter than E Wavedash.
Successive electrics build capped knockback so enemies drift out of range;
walls still pin them. Landing/recovery resets the build-up. Damage remains
8 base / 3 echo before modifiers. All previous content is included.

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
  assert json.loads(patch.read('resources/app/package.json'))['version']=='0.3.6'
  for base in bases:
   for name in full.namelist():
    if name.endswith('/'):continue
    relative=name.removeprefix(prefix)
    merged=patch.read(relative) if relative in selected else base.read(name)
    assert merged==full.read(name),f'Incomplete patch: {name}'
 print(json.dumps({'path':str(out),'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest(),'patchedFiles':len(selected),'supportedBases':len(bases),'mergedContents':'identical to full download'}))
finally:
 for base in bases:base.close()
