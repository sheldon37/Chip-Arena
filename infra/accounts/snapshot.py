"""Offline operator tooling for a provider SQL export, never a public export route."""
import argparse
import contextlib
import json
import os
import re
from pathlib import Path
import sqlite3
import stat
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
DURABLE = ('player_accounts', 'account_audit', 'account_xp', 'account_xp_awards', 'player_invites')
TRANSIENT = ('player_sessions', 'dev_login_rates', 'player_presence')
ROOMS = ('online_rooms', 'party_rooms', 'party_seats', 'relay_rooms')
NEW = ('steam_links', 'steam_ticket_uses')
META = {'d1_migrations', '__drizzle_migrations', '_cf_KV', 'sqlite_sequence'}


class Stop(Exception):
    """Messages must contain only fixed labels or allowlisted table names."""


def private_path(path, existing):
    path = Path(path).absolute()
    if path.parent.resolve() != path.parent or path.is_symlink() or path.resolve().is_relative_to(ROOT):
        raise Stop('Use a private path outside the repository without symlinks.')
    if path.parent.stat().st_uid != os.getuid() or stat.S_IMODE(path.parent.stat().st_mode) & 0o077:
        raise Stop('Use an owner-only directory (0700) on encrypted storage.')
    if existing:
        s = path.stat()
        if not stat.S_ISREG(s.st_mode) or s.st_uid != os.getuid() or s.st_mode & 0o077:
            raise Stop('Input must be an owner-only regular file (0600).')
    return path


@contextlib.contextmanager
def new_file(path, binary=False):
    path = private_path(path, False)
    with path.open('xb' if binary else 'x') as f:
        try:
            yield f
            f.flush()
            os.fsync(f.fileno())
        except BaseException:
            f.close()
            path.unlink(missing_ok=True)
            raise


def cms(args, **kw):
    r = subprocess.run(['openssl', 'cms', *args], stderr=subprocess.DEVNULL, **kw)
    if r.returncode:
        raise Stop('Encrypted backup operation failed. Verify the recipient certificate and recovery key.')
    return r


def backup(source, sealed, recipient):
    cert = private_path(recipient, True)
    with private_path(source, True).open('rb') as origin, new_file(sealed, binary=True) as out:
        cms(['-encrypt', '-binary', '-aes-256-gcm', '-outform', 'DER', '-recip', str(cert),
             '-keyopt', 'rsa_padding_mode:oaep', '-keyopt', 'rsa_oaep_md:sha256'], stdin=origin, stdout=out)


def database(sql):
    db = sqlite3.connect(':memory:')
    db.execute('PRAGMA trusted_schema=OFF')
    db.execute('PRAGMA temp_store=MEMORY')
    # Only process a trusted provider export. Prevent file access via ATTACH,
    # VACUUM INTO, virtual tables, extension loading or triggers in a dump.
    blocked = {sqlite3.SQLITE_ATTACH, sqlite3.SQLITE_DETACH, sqlite3.SQLITE_CREATE_TRIGGER,
               sqlite3.SQLITE_CREATE_VTABLE, sqlite3.SQLITE_CREATE_VIEW}
    def authorize(action, a, b, _db, _src):
        if action in blocked or (action == sqlite3.SQLITE_FUNCTION and b == 'load_extension'):
            return sqlite3.SQLITE_DENY
        if action == sqlite3.SQLITE_PRAGMA and a.lower() not in {'defer_foreign_keys', 'foreign_keys'}:
            return sqlite3.SQLITE_DENY
        return sqlite3.SQLITE_OK
    db.set_authorizer(authorize)
    db.executescript(sql)
    db.set_authorizer(None)
    if db.in_transaction:
        raise Stop('Snapshot has an unfinished transaction.')
    return db


def restored(path):
    path = private_path(path, True)
    key = private_path(os.environ.get('CHIP_BACKUP_KEY', ''), True)
    password = ['-passin', 'env:CHIP_BACKUP_PASSPHRASE'] if 'CHIP_BACKUP_PASSPHRASE' in os.environ else []
    raw = cms(['-decrypt', '-binary', '-inform', 'DER', '-in', str(path), '-inkey', str(key), *password],
              stdin=subprocess.DEVNULL, stdout=subprocess.PIPE).stdout
    return database(raw.decode('utf-8'))


def schema(destination):
    folder = HERE / 'migrations' if destination else ROOT / 'drizzle'
    return database('\n'.join(p.read_text() for p in sorted(folder.glob('*.sql'))))


def names(db):
    return {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")} - META


def layout(db, table):
    columns = list(db.execute(f'PRAGMA table_info("{table}")'))
    keys = list(db.execute(f'PRAGMA foreign_key_list("{table}")'))
    indexes = []
    for _, name, unique, origin, partial in db.execute(f'PRAGMA index_list("{table}")'):
        index_cols = tuple(r[2] for r in db.execute('SELECT * FROM pragma_index_info(?)', (name,)))
        indexes.append((unique, origin, partial, index_cols))
    return columns, keys, sorted(indexes)


def validate(db, destination, *, legacy_source=False):
    expected = schema(destination)
    if names(db) != names(expected):
        raise Stop('Unexpected or missing tables. Reconcile the schema before continuing.')
    for t in sorted(names(expected)):
        actual_layout, expected_layout = layout(db, t), layout(expected, t)
        if actual_layout == expected_layout:
            continue
        # Pre-practice Site and independent backups lack only the appended clear mask.
        cols, keys, indexes = expected_layout
        if (t == 'player_accounts' and (not destination or legacy_source)
                and cols[-1][1] == 'boss_clears'
                and actual_layout == (cols[:-1], keys, indexes)):
            continue
        raise Stop('Schema differs for ' + t)
    if db.execute('PRAGMA integrity_check').fetchone() != ('ok',):
        raise Stop('SQLite integrity check failed.')
    if db.execute('PRAGMA foreign_key_check').fetchone():
        raise Stop('Broken foreign key relationship.')
    if db.execute("""SELECT 1 FROM account_audit h WHERE NOT EXISTS
        (SELECT 1 FROM player_accounts a WHERE a.id=h.actor) OR NOT EXISTS
        (SELECT 1 FROM player_accounts a WHERE a.id=h.target) LIMIT 1""").fetchone():
        raise Stop('Unresolved administration history references.')
    if db.execute("""SELECT 1 FROM account_xp_awards w LEFT JOIN account_xp x ON x.account=w.account
        WHERE x.account IS NULL OR w.previous_total>w.new_total OR w.new_total>x.total LIMIT 1""").fetchone():
        raise Stop('Inconsistent XP receipt or balance.')
    for password, recovery in db.execute('SELECT password,recovery FROM player_accounts'):
        if not (isinstance(password, str) and re.fullmatch(r'scrypt\$32768\$8\$3\$[A-Za-z0-9_-]{22}\$[A-Za-z0-9_-]{43}', password)
                and isinstance(recovery, str) and re.fullmatch(r'[A-Za-z0-9_-]{43}', recovery)):
            raise Stop('Unrecognized stored credential format.')
    if db.execute("""SELECT 1 FROM player_accounts WHERE NOT json_valid(profile) OR
        NOT json_valid(unlocks) OR NOT json_valid(flags) LIMIT 1""").fetchone():
        raise Stop('An account has an unrecognized credential or profile format.')


def count(db, table):
    return db.execute(f'SELECT count(*) FROM "{table}"').fetchone()[0]


def source_tables(source):
    independent = 'steam_links' in names(source)
    validate(source, independent, legacy_source=True)
    return DURABLE + NEW if independent else DURABLE


def emit_import(source, output):
    tables = source_tables(source)
    # A failed guard aborts Wrangler's remote file import. Never merge, truncate
    # or overwrite an existing database, including a partially used destination.
    with new_file(output) as out:
        out.write('CREATE TABLE chip_import_guard (n INTEGER CHECK(n=0));\n')
        for t in DURABLE + TRANSIENT + NEW:
            out.write(f'INSERT INTO chip_import_guard SELECT count(*) FROM "{t}";\n')
        for t in tables:
            cols = [r[1] for r in source.execute(f'PRAGMA table_info("{t}")')]
            quoted = ','.join(f'"{col}"' for col in cols)
            # hex for text preserves Unicode, NUL, quotes and control characters;
            # one statement per row, never a diagnostic or a command-line arg.
            expressions = ','.join(f"""CASE typeof("{c}") WHEN 'text' THEN
                'CAST(X'''||hex(CAST("{c}" AS BLOB))||''' AS TEXT)' ELSE quote("{c}") END""" for c in cols)
            for values in source.execute(f'SELECT {expressions} FROM "{t}"'):
                statement = f'INSERT INTO "{t}" ({quoted}) VALUES (' + ','.join(values) + ');\n'
                if len(statement.encode()) > 100000:
                    raise Stop('Row exceeds D1 statement limit in ' + t)
                out.write(statement)
        out.write('DROP TABLE chip_import_guard;\n')


def verify(source, dest):
    tables = source_tables(source)
    validate(dest, True)
    report = {}
    for t in tables:
        info = list(source.execute(f'PRAGMA table_info("{t}")'))
        pk = [r[0] for r in sorted(info, key=lambda r: r[5]) if r[5]]
        order = ','.join(f'"{info[i][1]}"' for i in pk)
        query = f'SELECT * FROM "{t}" ORDER BY {order}'
        left, right = iter(source.execute(query)), iter(dest.execute(query))
        a, b = next(left, None), next(right, None)
        missing = extra_count = 0
        mismatches = {}
        while a is not None or b is not None:
            ka = tuple(a[i] for i in pk) if a else None
            kb = tuple(b[i] for i in pk) if b else None
            if b is None or (a is not None and ka < kb):
                missing += 1
                a = next(left, None)
            elif a is None or kb < ka:
                extra_count += 1
                b = next(right, None)
            else:
                for i, column in enumerate(info):
                    if a[i] != b[i] or type(a[i]) != type(b[i]):
                        name = column[1]
                        mismatches[name] = mismatches.get(name, 0) + 1
                if t == 'player_accounts' and len(b) == len(a)+1 and b[-1] != 0:
                    mismatches['boss_clears'] = mismatches.get('boss_clears', 0)+1
                a, b = next(left, None), next(right, None)
        report[t] = dict(source=count(source,t), destination=count(dest,t), missing=missing,
                         extra=extra_count, field_mismatches=mismatches)
    extra = {t: count(dest,t) for t in TRANSIENT + NEW if t not in tables}
    ok = not any(r['missing'] or r['extra'] or r['field_mismatches'] for r in report.values()) and not any(extra.values())
    return dict(ok=ok, tables=report, destination_initial_rows=extra)


def cloud(stage, args, capture=False):
    cfg = HERE / f'wrangler.{stage}.local.json'
    c = json.loads(cfg.read_text())
    binding = c['d1_databases'][0]
    expected = 'chip-arena-accounts-' + stage
    db_name = os.environ.get('CHIP_D1_' + stage.upper() + '_NAME', expected)
    if db_name != expected and not (db_name.startswith(expected + '-') and re.fullmatch(r'[a-z0-9-]{1,63}', db_name)):
        raise Stop('Unrecognized database name for this environment.')
    if (c['name'] != expected or c['vars']['ACCOUNT_ENV'] != stage or
            c['account_id'] != os.environ.get('CLOUDFLARE_ACCOUNT_ID') or
            binding['database_id'] != os.environ.get('CHIP_D1_' + stage.upper() + '_ID') or
            binding['database_name'] != db_name or binding['binding'] != 'DB'):
        raise Stop('Environment, Worker or D1 binding does not match the operator configuration.')
    env = dict(os.environ, WRANGLER_WRITE_LOGS='false', WRANGLER_SEND_METRICS='false',
               WRANGLER_LOG_SANITIZE='true', WRANGLER_LOG='log', CI='true')
    cli = [str(HERE / 'node_modules/.bin/wrangler'), '-c', str(cfg), 'd1']
    def run(cmd, output):
        result = subprocess.run(cli + cmd, cwd=HERE, env=env, stdin=subprocess.DEVNULL,
                                stdout=output, stderr=subprocess.DEVNULL)
        if result.returncode:
            raise Stop('Wrangler operation failed. Verify D1 identity, permissions and target state.')
        return result.stdout
    info = json.loads(run(['info', db_name, '--json'], subprocess.PIPE))
    if info.get('uuid') != binding['database_id'] or info.get('name') != db_name:
        raise Stop('Remote database identity differs from config.')
    if args[0] == 'execute' and c['vars']['ACCOUNT_STATE'] != 'maintenance':
        raise Stop('Import requires maintenance configuration and a fenced, empty target.')
    return run(args, subprocess.PIPE if capture else subprocess.DEVNULL)


def transfer(args):
    stage = args.environment
    sealed = private_path(args.encrypted_output, False)
    private_path(args.recipient_certificate, True)
    if sealed.exists():
        raise Stop('Choose a new encrypted backup path.')
    with tempfile.TemporaryDirectory(dir=sealed.parent) as directory:
        work = Path(directory)
        dump = work / 'destination.sql'
        if args.command == 'import':
            source = restored(args.encrypted_source)
            cloud(stage, ['export', 'DB', '--remote', '--output', str(dump), '--skip-confirmation'])
            before = database(dump.read_text())
            validate(before, True)
            if any(count(before, t) for t in DURABLE + TRANSIENT + NEW):
                raise Stop('Destination already contains data. Verify it or provision a fresh target.')
            dump.unlink()
            script = work / 'accounts.sql'
            emit_import(source, script)
            cloud(stage, ['execute', 'DB', '--remote', '--file', str(script), '--yes'])
        cloud(stage, ['export', 'DB', '--remote', '--output', str(dump), '--skip-confirmation'])
        backup(dump, sealed, args.recipient_certificate)
        if args.command == 'import':
            report = verify(source, restored(sealed))
            print(json.dumps(report, indent=2))
            return 0 if report['ok'] else 1
        return 0


def main():
    os.umask(0o077)
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    b = sub.add_parser('backup')
    b.add_argument('provider_sql')
    b.add_argument('encrypted_output')
    b.add_argument('recipient_certificate')
    p = sub.add_parser('prepare')
    p.add_argument('encrypted_source')
    p.add_argument('private_import_sql')
    v = sub.add_parser('verify')
    v.add_argument('encrypted_source')
    v.add_argument('encrypted_destination')
    for action in ('import', 'remote-backup'):
        op = sub.add_parser(action)
        op.add_argument('environment', choices=['staging', 'production'])
        if action == 'import':
            op.add_argument('encrypted_source')
        op.add_argument('encrypted_output')
        op.add_argument('recipient_certificate')
    args = parser.parse_args()
    if args.command in ('import', 'remote-backup'):
        return transfer(args)
    if args.command == 'backup':
        backup(args.provider_sql, args.encrypted_output, args.recipient_certificate)
    elif args.command == 'prepare':
        emit_import(restored(args.encrypted_source), args.private_import_sql)
    else:
        report = verify(restored(args.encrypted_source), restored(args.encrypted_destination))
        print(json.dumps(report, indent=2))
        return 0 if report['ok'] else 1
    return 0


if __name__ == '__main__':
    try:
        sys.exit(main())
    except Stop as e:
        print(json.dumps({'ok': False, 'error': str(e)}), file=sys.stderr)
    except (OSError, sqlite3.Error, UnicodeError, ValueError):
        print('{"ok":false,"error":"File, SQL or encoding validation failed."}', file=sys.stderr)
    sys.exit(1)
