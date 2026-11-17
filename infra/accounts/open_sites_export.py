"""Convert an encrypted Sites account capture to the existing verified CMS backup format."""
import argparse
import base64
import json
import os
import tempfile
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import padding
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
import snapshot


def open_capture(envelope, key):
    if envelope['format'] != 'chip-account-export-1':
        raise ValueError('Unknown capture format')
    def decode(field):
        return base64.b64decode(envelope[field], validate=True)
    aes = key.decrypt(decode('wrapped'), padding.OAEP(padding.MGF1(hashes.SHA256()), hashes.SHA256(), None))
    payload = json.loads(AESGCM(aes).decrypt(decode('iv'), decode('sealed'), None))
    if set(payload['rows']) != set(snapshot.DURABLE):
        raise ValueError('Account table set mismatch')
    if any(obj['type'] not in ('table', 'index') for obj in payload['schema']):
        raise ValueError('Unexpected schema object')
    db = snapshot.database('\n'.join(obj['sql']+';' for obj in payload['schema'] if obj['sql']))
    snapshot.validate(db, False)
    for table in snapshot.DURABLE:
        columns = [r[1] for r in db.execute(f'PRAGMA table_info("{table}")')]
        placeholders = ','.join('?' for _ in columns)
        for row in payload['rows'][table]:
            if set(row) != set(columns):
                raise ValueError('Capture column mismatch')
            db.execute(f'INSERT INTO "{table}" VALUES ({placeholders})', [row[c] for c in columns])
    db.commit()
    snapshot.validate(db, False)
    return db


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('capture', 'private_key', 'recipient_certificate', 'output'):
        parser.add_argument(name)
    args = parser.parse_args()
    os.umask(0o077)
    try:
        password = os.environ.get('CHIP_BACKUP_PASSPHRASE')
        key = serialization.load_pem_private_key(snapshot.private_path(args.private_key, True).read_bytes(),
                                                 password.encode() if password else None)
        db = open_capture(json.loads(snapshot.private_path(args.capture, True).read_bytes()), key)
        output = snapshot.private_path(args.output, False)
        with tempfile.TemporaryDirectory(dir=output.parent) as work:
            sql = Path(work) / 'capture.sql'
            with snapshot.new_file(sql) as stream:
                for line in db.iterdump():
                    stream.write(line+'\n')
            snapshot.backup(sql, output, args.recipient_certificate)
    except Exception:
        raise SystemExit('Capture conversion failed; no import was attempted.')
