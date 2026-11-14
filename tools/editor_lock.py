"""Run a command under the shared mkdir Editor lock; status never removes unknown locks.

python tools/editor_lock.py --root 'F:/Chip Arena Development' --who Astra --task checks -- COMMAND ...
Queue is FIFO. .editor-priority may contain one waiting ticket id to promote it.
Legacy/unknown locks are reported and left for their holder; --reap removes only
locks whose recorded local process is provably dead (never merely old).
"""
import argparse
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
import uuid


def alive(pid):
    if os.name == 'nt':
        import ctypes
        kernel=ctypes.WinDLL('kernel32',use_last_error=True)
        kernel.OpenProcess.restype=ctypes.c_void_p
        handle=kernel.OpenProcess(0x1000,False,pid)
        if not handle:
            return ctypes.get_last_error()!=87  # access denied is not proof of death
        try:
            code=ctypes.c_ulong()
            if not kernel.GetExitCodeProcess(ctypes.c_void_p(handle),ctypes.byref(code)):
                return True
            return code.value==259
        finally:kernel.CloseHandle(ctypes.c_void_p(handle))
    try:os.kill(pid,0);return True
    except ProcessLookupError:return False
    except PermissionError:return True


def read(path):
    try:return json.loads(path.read_text(encoding='utf-8'))
    except (OSError,ValueError):return None


def dead(record):
    return (isinstance(record,dict) and record.get('host')==socket.gethostname()
            and isinstance(record.get('pid'),int) and record['pid']>0 and not alive(record['pid']))


def reap(lock):
    record=read(lock/'holder.json')
    if not dead(record):return False
    # A known dead holder cannot release/reacquire. Rename atomically before cleanup.
    tomb=lock.with_name(lock.name+'.stale-'+uuid.uuid4().hex)
    try:lock.rename(tomb)
    except FileNotFoundError:return False
    # Do not recursively remove a legacy holder's files.
    (tomb/'holder.json').unlink();tomb.rmdir();return True


def first(queue,priority):
    tickets=[]
    for p in queue.glob('*.json'):
        data=read(p)
        if dead(data):
            try:p.unlink()
            except FileNotFoundError:pass
        elif isinstance(data,dict) and isinstance(data.get('queued_ns'),int):
            tickets.append((data['queued_ns'],p.name))
    tickets.sort()
    try:chosen=priority.read_text(encoding='utf-8').strip()
    except OSError:chosen=''
    if chosen and any(name==chosen for _,name in tickets):return chosen
    return tickets[0][1] if tickets else None


def run(root,who,task,command,parent=None,timeout=None,do_reap=False):
    root=Path(root);root.mkdir(parents=True,exist_ok=True)
    lock=root/'.editor-lock';queue=root/'.editor-queue';queue.mkdir(exist_ok=True)
    priority=root/'.editor-priority';token=uuid.uuid4().hex;ticket=queue/(token+'.json')
    data={'who':who,'task':task,'pid':os.getpid(),'parent':parent or os.getppid(),
          'host':socket.gethostname(),'queued_ns':time.time_ns(),'token':token}
    temp=queue/(token+'.writing');temp.write_text(json.dumps(data),encoding='utf-8');temp.replace(ticket)
    acquired=False;child=None;start=time.monotonic()
    try:
        print('Editor queue ticket: '+ticket.name,flush=True)
        while not acquired:
            if not alive(data['parent']):return 125
            if timeout is not None and time.monotonic()-start>=timeout:return 124
            if first(queue,priority)==ticket.name:
                if do_reap:reap(lock)
                try:lock.mkdir();acquired=True
                except FileExistsError:pass
            if not acquired:time.sleep(.2)
        data['since']=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())
        (lock/'holder.json').write_text(json.dumps(data,indent=2),encoding='utf-8')
        ticket.unlink()
        child=subprocess.Popen(command)
        while child.poll() is None:
            if not alive(data['parent']):
                child.terminate()
                try:child.wait(timeout=5)
                except subprocess.TimeoutExpired:child.kill();child.wait()
                return 125
            time.sleep(.2)
        return child.returncode
    finally:
        if child is not None and child.poll() is None:
            child.terminate()
            try:child.wait(timeout=5)
            except subprocess.TimeoutExpired:child.kill();child.wait()
        ticket.unlink(missing_ok=True)
        if acquired:
            holder=read(lock/'holder.json')
            if holder and holder.get('token')==token:
                (lock/'holder.json').unlink();lock.rmdir()


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--root',type=Path,required=True);p.add_argument('--who',default='agent');p.add_argument('--task',default='Editor task')
    p.add_argument('--status',action='store_true');p.add_argument('--reap',action='store_true');p.add_argument('--timeout',type=float)
    p.add_argument('--parent-pid',type=int);p.add_argument('command',nargs=argparse.REMAINDER)
    a=p.parse_args();lock=a.root/'.editor-lock'
    if a.status:
        holder=read(lock/'holder.json');print(json.dumps({'locked':lock.exists(),'holder':holder,'known_dead':dead(holder),'next':first(a.root/'.editor-queue',a.root/'.editor-priority')},indent=2));return 0
    command=a.command[1:] if a.command[:1]==['--'] else a.command
    if not command:p.error('a command after -- is required')
    return run(a.root,a.who,a.task,command,a.parent_pid,a.timeout,a.reap)

if __name__=='__main__':
    try:sys.exit(main())
    except KeyboardInterrupt:sys.exit(130)
