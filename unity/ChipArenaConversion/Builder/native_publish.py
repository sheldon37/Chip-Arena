"""Publish validated native packages without a mutable channel working checkout."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import uuid

CHANNEL_REPO = 'https://github.com/sheldon37/chip-arena-test-updates.git'
FEED_PATH = 'native/windows-x64/latest.json'
REMOTE_REF = 'refs/remotes/origin/main'


class Channel:
    def __init__(self, path, run):
        self.path = path
        self.run = run

    def git(self, *args, **kwargs):
        return self.run(['git', '-C', self.path, *args], stdout=subprocess.PIPE,
                        **kwargs).stdout

    def text(self, *args):
        return self.git(*args).decode('utf-8').strip()

    def prepare(self):
        if not self.path.exists():
            self.path.parent.mkdir(parents=True, exist_ok=True)
            temporary = self.path.with_name('publisher-init-' + uuid.uuid4().hex + '.git')
            self.run(['git', 'init', '--bare', temporary], stdout=subprocess.PIPE)
            self.run(['git', '-C', temporary, 'remote', 'add', 'origin', CHANNEL_REPO])
            temporary.replace(self.path)
        if self.text('rev-parse', '--is-bare-repository') != 'true':
            raise ValueError('The publisher cache is not a bare repository; it was left unchanged.')
        if self.text('remote', 'get-url', 'origin') != CHANNEL_REPO:
            raise ValueError('The publisher cache points to another repository; it was left unchanged.')
        # Download trees and the current manifest, not every old binary or the
        # complete 1.7+ GiB history. Existing v1 checkouts are never touched.
        self.git('config', 'remote.origin.promisor', 'true')
        self.git('config', 'remote.origin.partialclonefilter', 'blob:none')
        return self.refresh()

    def refresh(self):
        self.git('fetch', '--depth=1', '--filter=blob:none', '--no-tags', 'origin',
                 '+refs/heads/main:' + REMOTE_REF)
        return self.text('rev-parse', REMOTE_REF + '^{commit}')

    def manifest(self, commit, validator):
        if not self.git('ls-tree', commit, '--', FEED_PATH):
            return None
        return validator.read_manifest(self.git('show', commit + ':' + FEED_PATH))

    def blob(self, data):
        return self.git('hash-object', '-w', '--stdin', input=data).strip()

    def tree(self, old_tree, changes):
        # Byte paths preserve every unrelated entry, including non-ASCII names.
        entries = {}
        if old_tree:
            for row in self.git('ls-tree', '-z', old_tree).split(b'\0'):
                if row:
                    metadata, name = row.split(b'\t', 1)
                    entries[name] = metadata
        for name, value in changes.items():
            if isinstance(value, dict):
                old = entries.get(name)
                if old and not old.startswith(b'040000 tree '):
                    raise ValueError('Expected a directory in the native update channel.')
                subtree = self.tree(old.split(b' ')[2].decode() if old else None, value)
                entries[name] = b'040000 tree ' + subtree
            else:
                entries[name] = b'100644 blob ' + value
        data = b''.join(metadata + b'\t' + name + b'\0' for name, metadata in entries.items())
        # Existing promised blobs can remain on GitHub. Their IDs and all old
        # trees are preserved exactly; only the new native payload is uploaded.
        return self.git('mktree', '-z', '--missing', input=data).strip()

    def commit(self, parent, feed, manifest):
        parts = {}
        for part in manifest['parts']:
            data = (feed / 'objects' / part['name']).read_bytes()
            if len(data) != part['bytes'] or hashlib.sha256(data).hexdigest() != part['sha256']:
                raise ValueError('The build changed while preparing publication; nothing was pushed.')
            parts[part['name'].encode()] = self.blob(data)
        metadata = (feed / 'latest.json').read_bytes()
        if json.loads(metadata.decode('utf-8-sig')) != manifest:
            raise ValueError('The manifest changed while preparing publication; nothing was pushed.')
        changes = {b'native': {b'windows-x64': {
            b'objects': parts, b'latest.json': self.blob(metadata)}}}
        tree = self.tree(self.text('rev-parse', parent + '^{tree}'), changes).decode()
        # A deterministic automated identity avoids first-PC Git setup failures
        # and never publishes the owner's private email or changes global config.
        identity = dict(os.environ, GIT_AUTHOR_NAME='Chip Arena Builder',
                        GIT_AUTHOR_EMAIL='builder@chip-arena.invalid',
                        GIT_COMMITTER_NAME='Chip Arena Builder',
                        GIT_COMMITTER_EMAIL='builder@chip-arena.invalid')
        commit = self.git('commit-tree', tree, '-p', parent,
                          '-m', 'Publish Unity native test build ' + manifest['build']['version'][:12],
                          env=identity).decode().strip()
        self.git('update-ref', 'refs/builder/pending', commit)
        return commit

    def push(self, commit, size):
        # The owner's 91.6 MiB chunked POST returned HTTP 408; a buffered HTTP/1.1
        # push succeeded. Apply only to this operation, never global Git settings.
        buffer = max(128 * 1024**2, size + 16 * 1024**2)
        self.run(['git', '-C', self.path, '-c', 'http.version=HTTP/1.1',
                  '-c', 'http.postBuffer=' + str(buffer), 'push', '--verbose',
                  '--progress', 'origin', commit + ':refs/heads/main'])


def public_status(validator, expected):
    try:
        served = validator.remote_manifest()
        if served == expected:
            print('The public feed serves this build. Players can use Update Game.', flush=True)
            return True
        print('Published on GitHub. The public feed is still refreshing; use Check-Update-Status.cmd shortly.', flush=True)
    except (OSError, ValueError, KeyError, TypeError) as error:
        print('Published on GitHub. Public feed verification is pending: ' + str(error), flush=True)
    return False


def publish(feed, path, local, validator, run):
    if validator.validate(feed) != local:
        raise ValueError('The build changed before publication; nothing was pushed.')
    channel = Channel(Path(path), run)
    parent = channel.prepare()
    current = channel.manifest(parent, validator)
    if current and current['build']['version'] == local['build']['version']:
        print('Already published: ' + local['build']['version'][:12] + '. No duplicate update was uploaded.', flush=True)
        return dict(status='already-published', channelCommit=parent, build=current['build'],
                    publicFeedVerified=public_status(validator, current))
    problem = validator.publication_problem(local, current)
    if problem:
        raise ValueError(problem)
    commit = channel.commit(parent, feed, local)
    print('Uploading build ' + local['build']['version'][:12] + '...', flush=True)
    try:
        channel.push(commit, local['bytes'])
    except subprocess.CalledProcessError:
        # A dropped response can occur after the server accepted the push. Never
        # trust Git's misleading "Everything up-to-date" line or force a retry.
        head = channel.refresh()
        if channel.manifest(head, validator) != local:
            raise ValueError('Upload did not publish this build. Run Publish-Native.cmd to retry the saved build; no rebuild is needed.') from None
        commit = head
    head = channel.refresh()
    if channel.manifest(head, validator) != local:
        raise ValueError('The channel changed during publication. Use Check-Update-Status.cmd; no force push was attempted.')
    print('Published native Unity build ' + local['build']['version'], flush=True)
    print('Channel commit: ' + commit, flush=True)
    return dict(status='published', channelCommit=commit, build=local['build'],
                publicFeedVerified=public_status(validator, local))
