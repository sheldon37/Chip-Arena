"""Exercise the migration and actual HTTP Worker on disposable local Wrangler D1."""
import json
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
from unittest.mock import patch
import snapshot as s
from test_snapshot import fixture


def run(cmd, capture=False):
    r=subprocess.run(cmd, cwd=s.HERE, stdin=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                     stdout=subprocess.PIPE if capture else subprocess.DEVNULL)
    if r.returncode:
        raise RuntimeError('Local rehearsal command failed: ' + Path(cmd[0]).name)
    return r.stdout


def call(origin, path, body=None, session=None, expected=200):
    headers={'Content-Type':'application/json'}
    if session:
        headers['Authorization']='Bearer '+session
    req=urllib.request.Request(origin+path, data=None if body is None else json.dumps(body).encode(), headers=headers)
    try:
        response=urllib.request.urlopen(req,timeout=30)
    except urllib.error.HTTPError as e:
        response=e
    with response:
        if response.status!=expected:
            raise RuntimeError(f'Rehearsal HTTP {path}: expected {expected}, received {response.status}')
        return json.load(response)


def main(work):
    work=Path(work)
    password,recovery,issuer=secrets.token_hex(20),secrets.token_hex(20),secrets.token_hex(32)
    source=fixture(password,recovery)
    raw=work/'source.sql'
    raw.write_text('\n'.join(source.iterdump()))
    key, cert = work/'recovery.pem', work/'recipient.pem'
    run(['openssl','req','-x509','-newkey','rsa:3072','-nodes','-days','1','-subj','/CN=Disposable QA',
         '-keyout',str(key),'-out',str(cert)])
    os.environ['CHIP_BACKUP_KEY']=str(key)
    sealed,script=work/'source.cms',work/'import.sql'
    s.backup(raw,sealed,cert)
    restored=s.restored(sealed)
    s.emit_import(restored,script)
    cfg=json.loads((s.HERE/'wrangler.template.json').read_text())
    cfg['main']=str(s.ROOT/'src/relay/account-worker.ts')
    cfg['d1_databases'][0].update(database_id='00000000-0000-4000-8000-000000000001',migrations_dir=str(s.HERE/'migrations'))
    key=run(['node','-e',"""const {privateKey}=require('crypto').generateKeyPairSync('ec',{namedCurve:'P-256'});
process.stdout.write(JSON.stringify(privateKey.export({format:'jwk'})))"""],True).decode()
    cfg['vars'].update(ACCOUNT_ENV='development',ACCOUNT_STATE='active',DEV_SIGNING_KEY=key,XP_ISSUER_SECRET=issuer)
    conf=work/'wrangler.json'
    conf.write_text(json.dumps(cfg))
    cli=[str(s.HERE/'node_modules/.bin/wrangler'),'--cwd',str(work),'-c',str(conf)]
    local=['--local']
    run(cli+['d1','migrations','apply','DB',*local])
    run(cli+['d1','execute','DB',*local,'--file',str(script)])
    dump=work/'destination.sql'
    run(cli+['d1','export','DB',*local,'--output',str(dump)])
    sealed_dest=work/'destination.cms'
    s.backup(dump,sealed_dest,cert)
    compared=s.verify(restored,s.restored(sealed_dest))
    print(json.dumps(compared),flush=True)
    if not compared['ok']:
        raise RuntimeError('Round trip did not match.')
    with socket.socket() as port:
        port.bind(('127.0.0.1',0))
        number=port.getsockname()[1]
    run(cli+['deploy','--dry-run','--outdir',str(work/'bundle')])
    server=subprocess.Popen(['node',str(s.HERE/'local-worker.mjs'),str(work),str(number)],cwd=s.HERE,
        stdin=subprocess.DEVNULL,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,start_new_session=True)
    try:
        origin=f'http://127.0.0.1:{number}'
        for _ in range(100):
            try:
                if call(origin,'/healthz')['ready']:
                    break
            except urllib.error.URLError:
                if server.poll() is not None:
                    raise RuntimeError('Local Worker exited.')
                time.sleep(.2)
        else:
            raise RuntimeError('Local Worker did not become ready.')
        path='/api/accounts/'
        def api(action,*args,**kwargs):
            return call(origin,path+action,*args,**kwargs)
        login=api('login',{'username':'pilot_1','password':password})
        token=login['session']
        assert login['account']['id']=='id-1' and login['account']['revision']==7
        assert login['account']['flags']==['playtester','vip'] or set(login['account']['flags'])=={'vip','playtester'}
        assert api('xp',session=token)['summary']['total']==100
        api('profile',{'profile':{'name':'After restore'},'revision':7},token)
        assert api('me',session=token)['account']['profile']['name']=='After restore'
        api('grant',{},token,expected=403)
        dev=api('login',{'username':'pilot_2','password':password})['session']
        assert api('players',{'query':'pilot'},dev)['players']
        c=dict(version=1,completionId='new',mode='pvp',won=True,bots=False,seconds=70,cleared=0)
        api('xp',c,token,expected=503)
        call(origin,'/internal/match-completions',dict(accountId='id-1',completion=c),issuer)
        assert api('xp',c,token)['summary']['total']==250
        recovered=api('recover',dict(username='pilot_1',recoveryCode=recovery,password=secrets.token_hex(20)))
        assert recovered['account']['id']=='id-1'
        api('me',session=token,expected=401)
        api('logout',{},recovered['session'])
        api('me',session=recovered['session'],expected=401)
        print('Local workerd HTTP migration, permissions, recovery, profile and XP checks passed.')
    finally:
        if server.poll() is None:
            os.killpg(server.pid,signal.SIGTERM)
            server.wait(timeout=15)


if __name__=='__main__':
    os.umask(0o077)
    with tempfile.TemporaryDirectory() as d:
        with patch.dict(os.environ,WRANGLER_WRITE_LOGS='false',WRANGLER_SEND_METRICS='false',CI='true'):
            main(d)
