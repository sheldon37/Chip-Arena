import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest
import editor_lock as lock

class LockTests(unittest.TestCase):
    def setUp(self):self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name)
    def tearDown(self):self.tmp.cleanup()
    def start(self,task,code,extra=()):
        return subprocess.Popen([sys.executable,str(Path(lock.__file__)), '--root',str(self.root),'--who','test','--task',task,*extra,'--',sys.executable,'-c',code],stdout=subprocess.DEVNULL)
    def until(self,fn):
        deadline=time.monotonic()+5
        while not fn():
            if time.monotonic()>deadline:self.fail('timed out')
            time.sleep(.02)
    def test_serial_fifo_and_cleanup(self):
        out=self.root/'order';code=lambda label,delay:f"import time;from pathlib import Path;p=Path({str(out)!r});p.open('a').write({label!r});time.sleep({delay})"
        a=self.start('a',code('a',.8));self.until(lambda:(self.root/'.editor-lock/holder.json').exists())
        b=self.start('b',code('b',.1));self.until(lambda:len(list((self.root/'.editor-queue').glob('*.json')))==1)
        c=self.start('c',code('c',0))
        for p in (a,b,c):self.assertEqual(p.wait(timeout=6),0)
        self.assertEqual(out.read_text(),'abc');self.assertFalse((self.root/'.editor-lock').exists());self.assertEqual(list((self.root/'.editor-queue').glob('*.json')),[])
    def test_priority(self):
        q=self.root/'queue';q.mkdir()
        for name,n in [('old',1),('new',2)]:
            (q/(name+'.json')).write_text(json.dumps({'queued_ns':n,'pid':os.getpid(),'host':lock.socket.gethostname()}))
        priority=self.root/'priority';self.assertEqual(lock.first(q,priority),'old.json')
        priority.write_text('new.json');self.assertEqual(lock.first(q,priority),'new.json')
    def test_legacy_timeout_and_parent_exit(self):
        (self.root/'.editor-lock').mkdir()
        p=self.start('wait','raise Exception("must not run")',('--timeout','.2'))
        self.assertEqual(p.wait(timeout=3),124);self.assertTrue((self.root/'.editor-lock').is_dir())
        parent=subprocess.Popen([sys.executable,'-c','pass']);parent.wait()
        p=self.start('orphan','raise Exception("must not run")',('--parent-pid',str(parent.pid)))
        self.assertEqual(p.wait(timeout=3),125);self.assertEqual(list((self.root/'.editor-queue').glob('*.json')),[])
    def test_dead_holder_only(self):
        directory=self.root/'.editor-lock';directory.mkdir();self.assertFalse(lock.reap(directory))
        child=subprocess.Popen([sys.executable,'-c','pass']);child.wait()
        (directory/'holder.json').write_text(json.dumps({'pid':child.pid,'host':lock.socket.gethostname()}))
        self.assertTrue(lock.reap(directory));self.assertFalse(directory.exists())

if __name__=='__main__':unittest.main()
