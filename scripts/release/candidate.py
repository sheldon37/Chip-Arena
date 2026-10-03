"""Build an unpublished candidate from an exact commit in an isolated checkout.
No publishing command exists here. --test-only permits a diagnostic artifact after
failed checks but retains the failures and exits nonzero. Never a release bypass.
"""
import argparse,hashlib,json,os,re,shutil,subprocess,sys,tempfile,time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--commit',required=True);p.add_argument('--output',required=True);p.add_argument('--test-only',action='store_true');p.add_argument('--runtime-cache');p.add_argument('--channel');a=p.parse_args()
repo=Path(__file__).resolve().parents[2];out=Path(a.output).resolve();tool=Path(__file__).resolve().parent
if not re.fullmatch('[0-9a-f]{40}',a.commit):p.error('--commit must be a full 40-character Git SHA')
def git(*args):return subprocess.check_output(['git','-C',str(repo),*args],text=True).strip()
if git('status','--porcelain'):raise SystemExit('Tooling checkout must be clean; commit changes first.')
if git('rev-parse',a.commit+'^{commit}')!=a.commit:raise SystemExit('Commit is not available locally; fetch it first.')
if out.exists():raise SystemExit('Output path already exists. Use a new path; never overwrite a candidate.')
out.mkdir(parents=True);(out/'logs').mkdir()
source=Path(tempfile.mkdtemp(prefix='chip-release-'))/'source'
report={'sourceCommit':a.commit,'sourceTree':git('rev-parse',a.commit+'^{tree}'),'toolingCommit':git('rev-parse','HEAD'),'repository':'https://github.com/sheldon37/Chip-Arena','approval':'PENDING OWNER REVIEW','publishable':False,'testOnly':a.test_only,'createdUTC':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),'checks':{},'notRun':['Native Windows EXE/GPU','Real two-machine multiplayer','Installed-player old-to-new upgrade','Live publication (not authorized)']}
env=os.environ.copy();env['CHIP_RELEASE_DIR']=str(out/'packages');env['CHIP_PYTHON']=sys.executable
if a.channel:
 if not(a.channel.startswith('https://') and a.channel.endswith('/')):raise SystemExit('--channel must be an https URL ending in /')
 # Test-channel profile (game packager with updater v2): schema 1 may use the 32 MiB cap every installed launcher
 # enforces (live keeps 25 MiB), schema 2 (latest-v2.json) is added, and its launcher files are this channel's own.
 # Older game commits ignore these variables and build schema 1 exactly as before.
 overlay=source.parent/'launcher-overlay';overlay.mkdir()
 shutil.copyfile(tool/'test-remote-update.mjs',overlay/'manual-update.mjs');shutil.copyfile(tool/'remote-feed.mjs',overlay/'remote-feed.mjs')
 env['CHIP_UPDATE_PROFILE']='test-channel';env['CHIP_UPDATE_LAUNCHER_OVERLAY']=str(overlay)
npm=shutil.which('npm.cmd' if os.name=='nt' else 'npm');node=shutil.which('node')
if not npm or not node:raise SystemExit('Node/npm must be installed.')
def run(name,args,timeout=1200):
 print('Running '+name,flush=True)
 with (out/'logs'/f'{name}.log').open('w') as log:
  try:code=subprocess.run(args,cwd=source,env=env,stdout=log,stderr=subprocess.STDOUT,timeout=timeout).returncode
  except subprocess.TimeoutExpired:code=124
 report['checks'][name]={'exitCode':code,'result':'passed' if code==0 else 'failed'}
 print(f'{name}: exit {code}',flush=True);return code==0
try:
 subprocess.run(['git','-C',str(repo),'worktree','add','--detach',str(source),a.commit],check=True)
 expected=(source/'.node-version').read_text().strip();actual=subprocess.check_output([node,'--version'],text=True).strip().removeprefix('v')
 expected_npm=json.loads((source/'package.json').read_text())['packageManager'].split('@')[1]
 actual_npm=subprocess.check_output([npm,'--version'],text=True).strip()
 report['environment']={'node':actual,'npm':actual_npm,'python':sys.version.split()[0],'platform':sys.platform}
 if actual!=expected or actual_npm!=expected_npm:raise RuntimeError(f'Use pinned Node {expected} and npm {expected_npm}; found {actual}/{actual_npm}')
 if not run('setup',[npm,'run','setup']):raise RuntimeError('Dependency restoration failed')
 if not run('fixture-tests',[node,'--test',str(tool/'test-update-status.test.cjs'),str(tool/'test-remote-channel.test.mjs')],180):raise RuntimeError('Test fixture regression failed')
 gate=run('verify',[npm,'run','verify'],1200)
 targeted=run('updater-tests',[node,'--import','tsx','--test','tests/auth-updater.test.ts','tests/manual-updater.test.ts']+(['tests/update-v2.test.ts'] if (source/'tests/update-v2.test.ts').exists() else []),300)
 if not(gate and targeted) and not a.test_only:raise RuntimeError('Verification failed; packaging blocked. --test-only is for diagnostics, never publication.')
 if a.runtime_cache:
  (source/'.cache/runtimes').mkdir(parents=True)
  for name in ['node-v24.21.0-win-x64.zip','electron-v44.4.5-win32-x64.zip','cloudflared-windows-amd64.exe']:
   shutil.copyfile(Path(a.runtime_cache)/name,source/'.cache/runtimes'/name)
 for name,cmd in [('runtime-pins',[npm,'run','runtime:fetch']),('build',[npm,'run','release:prepare'])]:
  if not run(name,cmd):raise RuntimeError(name+' failed')
 feed=out/'feed';shutil.copytree(source/'dist/client/updates',feed)
 # latest.json (schema 1, every launcher) is absent only when a test-channel build held it back for size.
 original={f.name:f.read_bytes() for f in feed.iterdir()};v1=feed/'latest.json';v2=feed/'latest-v2.json'
 manifest=json.loads((v1 if v1.exists() else v2).read_bytes())
 packager=source/'.cache/release/update-report.json'
 if packager.exists():report['packager']=json.loads(packager.read_text())
 if not run('repeat-build',[npm,'run','release:prepare']):raise RuntimeError('Repeat build failed')
 again={f.name:f.read_bytes() for f in (source/'dist/client/updates').iterdir()}
 same=again==original
 report['checks']['repeat-payload']={'result':'passed' if same else 'failed'}
 if not same:raise RuntimeError('Repeated update payload is not byte-identical')
 if not run('actual-payload',[node,str(tool/'check-payload.mjs'),str(feed),str(source)]):raise RuntimeError('Actual update payload validation failed')
 for name,cmd in [('windows-package',[npm,'run','package:windows']),('desktop-package',[npm,'run','package:desktop'])]:
  if not run(name,cmd):raise RuntimeError(name+' failed')
 if not run('isolated-test-package',[sys.executable,str(tool/'make-test-package.py'),str(out/'packages/Chip-Arena-Desktop.zip'),str(feed),str(out/'Chip-Arena-TEST-ONLY.zip'),str(source)]+(['--channel',a.channel] if a.channel else [])):raise RuntimeError('Test package creation failed')
 # The seed ZIP may be shared and the channel is public: neither may carry the editable source.
 for name,cmd in [('source-privacy-zip',[sys.executable,str(tool/'source_privacy.py'),str(out/'Chip-Arena-TEST-ONLY.zip')]),('source-privacy-feed',[node,str(tool/'source-privacy.mjs'),str(feed)])]:
  if not run(name,cmd,120):raise RuntimeError('A TEST-ONLY artifact contains game source ('+name+')')
 report['update']=manifest if v1.exists() else None;report['updateV2']=json.loads(v2.read_bytes()) if v2.exists() else None
 report['payloadBytes']={'schema1':manifest['bytes'] if v1.exists() else None,'schema2':report['updateV2']['bytes'] if v2.exists() else None}
 if a.channel and v2.exists():
  # The candidate's own seed ZIP and feed through the real Update path (desktop update.cjs spawning the channel entry,
  # remote-feed.mjs, launcher self-update, probation, rollback and hosting) against a local HTTPS copy of the channel.
  e2e=Path(tempfile.mkdtemp(prefix='chip-e2e-'))
  try:
   if not run('channel-e2e',[node,str(tool/'channel-e2e.mjs'),'--candidate',str(out),'--game',str(source),'--work',str(e2e/'work'),'--python',sys.executable],1800):raise RuntimeError('Channel end-to-end check failed')
  finally:
   if (e2e/'work/e2e-report.json').exists():shutil.copyfile(e2e/'work/e2e-report.json',out/'logs/channel-e2e-report.json')
   shutil.rmtree(e2e,ignore_errors=True)
 report['status']='BLOCKED' if not(gate and targeted) else 'AWAITING WINDOWS VALIDATION AND APPROVAL'
 # Full standard desktop ZIP is an intermediate, not the user-facing test artifact.
 shutil.rmtree(out/'packages')
except Exception as e:
 report['status']='BLOCKED';report['error']=str(e);print(str(e),file=sys.stderr)
finally:
 if (source/'test-results').exists():shutil.copytree(source/'test-results',out/'logs/verification',dirs_exist_ok=True)
 report['artifacts']={str(f.relative_to(out)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(out.rglob('*')) if f.is_file()}
 (out/'candidate.json').write_text(json.dumps(report,indent=2)+'\n')
 subprocess.run(['git','-C',str(repo),'worktree','remove','--force',str(source)],check=False)
 shutil.rmtree(source.parent,ignore_errors=True)
print(json.dumps({'output':str(out),'status':report['status'],'publishable':False}),flush=True)
sys.exit(1 if report['status']=='BLOCKED' else 0)
