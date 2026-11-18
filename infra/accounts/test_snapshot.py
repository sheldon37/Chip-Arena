import base64
import hashlib
import json
import os
import sqlite3
import tempfile
import unittest
from pathlib import Path
import snapshot as s


def b64(value):
    return base64.urlsafe_b64encode(value).decode().rstrip('=')


def fixture(password='generated-for-unit-tests', recovery='fixture-recovery'):
    db = s.schema(False)
    salt = os.urandom(16)
    encoded = 'scrypt$32768$8$3$' + b64(salt) + '$' + b64(hashlib.scrypt(
        password.encode(), salt=salt, n=32768, r=8, p=3, dklen=32, maxmem=48*1024*1024))
    recovery_hash = b64(hashlib.sha256(recovery.encode()).digest())
    for n in (1, 2):
        profile = json.dumps({'name': "O'Brien 雪", 'stats': {'wins': 4}, 'cosmetics': {'hat': 'cap'}})
        db.execute('INSERT INTO player_accounts(id,username,password,recovery,developer,unlocks,flags,profile,revision,grant_version,created,updated) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)', (
            f'id-{n}', f'pilot_{n}', encoded, recovery_hash, n-1, '["weapon:leet"]',
            '["playtester","vip"]', profile, 7, 3, 1, 2))
    db.execute("INSERT INTO account_audit VALUES ('audit','id-2','id-1','grant','{}',1)")
    db.execute("INSERT INTO account_xp VALUES ('id-1',100,1)")
    payload = json.dumps(dict(version=1,completionId='old',mode='pvp',won=False,bots=False,seconds=61,cleared=0))
    db.execute("INSERT INTO account_xp_awards VALUES ('id-1','old',?,0,100,'marker',1)", (payload,))
    db.execute("INSERT INTO player_invites VALUES ('invite','id-2','id-1','UNABCDEF',1,2,'declined')")
    db.execute("INSERT INTO player_sessions VALUES ('fixture-old-session','id-1',9000000000000)")
    db.execute("INSERT INTO player_presence VALUES ('id-2',1)")
    db.execute("INSERT INTO dev_login_rates VALUES ('throttle',1,1)")
    db.execute("INSERT INTO online_rooms VALUES ('UNABCDEF','lease','creator','https://invalid.test',2)")
    db.commit()
    return db


class SnapshotTests(unittest.TestCase):
    def setUp(self):
        self.work = tempfile.TemporaryDirectory()
        self.addCleanup(self.work.cleanup)
        self.sql = Path(self.work.name) / 'import.sql'
        self.source, self.dest = fixture(), s.schema(True)
        self.addCleanup(self.source.close)
        self.addCleanup(self.dest.close)

    def copy(self):
        s.emit_import(self.source, self.sql)
        self.dest.executescript(self.sql.read_text())

    def test_every_field_and_explicit_temporary_exclusions(self):
        self.copy()
        self.assertTrue(s.verify(self.source,self.dest)['ok'])
        self.assertEqual(s.count(self.dest,'player_sessions'),0)
        self.assertFalse(set(s.ROOMS) & s.names(self.dest))

    def test_sites_migration_metadata_is_source_only(self):
        ddl = 'CREATE TABLE __appgarden_migrations (id INTEGER PRIMARY KEY)'
        self.source.execute(ddl)
        self.copy()
        self.assertTrue(s.verify(self.source, self.dest)['ok'])
        self.dest.execute(ddl)
        with self.assertRaisesRegex(s.Stop, 'Unexpected or missing tables'):
            s.verify(self.source, self.dest)

    def test_earned_bosses_survive_copy(self):
        self.source.execute("UPDATE player_accounts SET boss_clears=33 WHERE id='id-1'")
        self.copy()
        self.assertTrue(s.verify(self.source, self.dest)['ok'])
        self.assertEqual(self.dest.execute("SELECT boss_clears FROM player_accounts WHERE id='id-1'").fetchone(), (33,))

    def test_pre_practice_snapshot_defaults_to_no_clears(self):
        self.source.execute('ALTER TABLE player_accounts DROP COLUMN boss_clears')
        self.copy()
        self.assertTrue(s.verify(self.source, self.dest)['ok'])
        self.dest.execute("UPDATE player_accounts SET boss_clears=1 WHERE id='id-1'")
        self.assertFalse(s.verify(self.source, self.dest)['ok'])

    def test_reject_changed_schema(self):
        self.source.execute('ALTER TABLE player_accounts ADD COLUMN score INTEGER')
        with self.assertRaisesRegex(s.Stop,'Schema differs'):
            s.emit_import(self.source,self.sql)
        self.assertFalse(self.sql.exists())

    def test_extra_table_blocks_silent_loss(self):
        self.source.execute('CREATE TABLE friends(a TEXT)')
        with self.assertRaisesRegex(s.Stop,'tables'):
            s.emit_import(self.source,self.sql)

    def test_nonempty_target_aborts(self):
        self.copy()
        with self.assertRaises(sqlite3.IntegrityError):
            self.dest.executescript(self.sql.read_text())
        self.assertEqual(s.count(self.dest,'player_accounts'),2)

    def test_no_clobber_partial_export(self):
        self.sql.write_text('existing')
        with self.assertRaises(FileExistsError):
            s.emit_import(self.source,self.sql)
        self.assertEqual(self.sql.read_text(),'existing')

    def test_comparison_reports_fields_without_values(self):
        self.copy()
        self.dest.execute("UPDATE player_accounts SET profile='{}', revision=8 WHERE id='id-1'")
        self.dest.execute("DELETE FROM player_invites")
        r=s.verify(self.source,self.dest)
        self.assertFalse(r['ok'])
        self.assertEqual(r['tables']['player_accounts']['field_mismatches'],{'profile':1,'revision':1})
        self.assertEqual(r['tables']['player_invites']['missing'],1)
        self.assertNotIn('pilot_1',json.dumps(r))
        self.assertNotIn('scrypt$',json.dumps(r))

    def test_post_cutover_recovery_preserves_links_and_replay_markers(self):
        self.copy()
        self.dest.execute("INSERT INTO steam_links VALUES ('id-1','76561198000000001',123)")
        self.dest.execute("INSERT INTO steam_ticket_uses VALUES ('used',123)")
        recovery = Path(self.work.name) / 'restore.sql'
        s.emit_import(self.dest,recovery)
        restored = s.schema(True)
        self.addCleanup(restored.close)
        restored.executescript(recovery.read_text())
        self.assertTrue(s.verify(self.dest,restored)['ok'])
        self.assertEqual(s.count(restored,'steam_links'),1)

    def test_orphan_fails(self):
        self.source.execute("UPDATE account_audit SET target='absent'")
        with self.assertRaisesRegex(s.Stop,'history'):
            s.emit_import(self.source,self.sql)


if __name__=='__main__':
    unittest.main()
