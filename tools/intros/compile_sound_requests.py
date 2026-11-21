"""compile_sound_requests.py: the intros' generated-sound requests (docs/audio/intros/README.md).

Merges every author's rows of one batch ({"author": "...", "sounds": [<the manifest's sound entries>]}) into that batch's manifest
on the existing schema, and its byte-identical bundle copy under unity/ChipArenaConversion/tools/sound_bundle/docs/audio/:
- `--batch b1` (the default): docs/audio/intros/*.json -> docs/audio/sound-requests-intros.json (batch 1 and Slipstream, shipped);
- `--batch b2`: docs/audio/intros-b2/*.json -> docs/audio/sound-requests-intros-b2.json (batch 2, the 15 weapon intros).
It checks each key: "intro2.<intro>.<cue>", letters/digits/dots, unique across every batch, and in Core/NativeIntroSoundKeys.cs with a
cap at least its targetSeconds.max. A manifest that an approved job of docs/audio/sound-queue.json pins (its manifestSha256) is sealed:
tools/sound_queue.py re-checks every approved job, finished ones too, against that hash, so changed bytes there fail the builder at the
sound queue. The compiler refuses to write them; new rows go in a new batch. With --job ID it also prints the approved builder job
(manifestSha256 after CRLF->LF, every key, variants, limits) to add to docs/audio/sound-queue.json and the bundle copy. --check says
whether the batch's manifest and its bundle copy are current. --root compiles another tree (the tests' copies). It never writes the
queue itself, makes no network call and needs no key.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import sys

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
KEY = re.compile(r"^intro2\.[a-z0-9]+(\.[a-z0-9]+)+$")
PURPOSE = ("Intros v2 (owner 2026-11-20: generation approved for the new intros): each intro's cue sounds. Until imported, "
           "every cue plays its existing bank key or built-in clip (NativeIntroCue.Alt / Clip). Contract: "
           "docs/audio/SOUND-REQUESTS.md; requests: docs/audio/intros/README.md.")
# (batch: the rows' folder and the manifest under docs/audio, and its purpose line). Batch 1's is as shipped: its manifest is sealed.
BATCHES = {
    "b1": ("intros", "sound-requests-intros.json", PURPOSE),
    "b2": ("intros-b2", "sound-requests-intros-b2.json",
           "Intros v2, batch 2: the 15 weapon intros (owner 2026-11-20: generation approved for the new intros): each intro's cue sounds. "
           "Batch 1's manifest (sound-requests-intros.json) is pinned by its approved jobs and stays as it is. Until imported, every cue "
           "plays its existing bank key or built-in clip (NativeIntroCue.Alt / Clip). Contract: docs/audio/SOUND-REQUESTS.md; requests: "
           "docs/audio/intros/README.md."),
}


def paths(root, batch):
    """The batch's rows folder, manifest, bundle copy and the manifest's queue path (docs/audio/...)."""
    rows, name, _ = BATCHES[batch]
    return (os.path.join(root, "docs", "audio", rows), os.path.join(root, "docs", "audio", name),
            os.path.join(root, "unity", "ChipArenaConversion", "tools", "sound_bundle", "docs", "audio", name), "docs/audio/" + name)


def lf_sha(data):
    return hashlib.sha256(data.replace(b"\r\n", b"\n")).hexdigest()


def pins(root, manifest):
    """The approved queue jobs that pin `manifest` (docs/audio/...): {manifestSha256: [job ids]}."""
    queue = os.path.join(root, "docs", "audio", "sound-queue.json")
    found = {}
    if os.path.exists(queue):
        for job in json.load(open(queue, encoding="utf-8")).get("jobs", []):
            if job.get("approved") is True and job.get("manifest") == manifest:
                found.setdefault(job.get("manifestSha256"), []).append(job.get("id"))
    return found


def rows_of(root, batch):
    for path in sorted(glob.glob(os.path.join(paths(root, batch)[0], "*.json"))):
        yield os.path.basename(path), json.load(open(path, encoding="utf-8"))


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--batch", default="b1", choices=sorted(BATCHES))
    ap.add_argument("--job")
    ap.add_argument("--check", action="store_true")
    ap.add_argument("--root", default=REPO)
    a = ap.parse_args(argv)
    root = os.path.abspath(a.root)
    folder, out, bundle, rel = paths(root, a.batch)
    head = json.load(open(os.path.join(root, "docs", "audio", "sound-requests-install.json"), encoding="utf-8"))
    source = open(os.path.join(root, "unity", "ChipArenaConversion", "Assets", "ChipArena", "Core", "NativeIntroSoundKeys.cs"),
                  encoding="utf-8").read()
    contract = {m.group(1): float(m.group(2)) for m in re.finditer(r'K\("([^"]+)",([0-9.]+)f', source)}
    # (a key belongs to one batch only: every batch's manifest feeds the one effects bank)
    elsewhere = {}
    for other in sorted(BATCHES):
        if other != a.batch:
            for name, rows in rows_of(root, other):
                for s in rows.get("sounds", []):
                    elsewhere.setdefault(s.get("key", ""), "%s (%s)" % (other, name))
    sounds, seen, errors = [], set(), []
    for name, rows in rows_of(root, a.batch):
        for s in rows.get("sounds", []):
            k = s.get("key", "")
            if not KEY.match(k):
                errors.append("%s: bad key %r" % (name, k))
            if k in seen:
                errors.append("%s: duplicate key %s" % (name, k))
            if k in elsewhere:
                errors.append("%s: %s is already requested by batch %s" % (name, k, elsewhere[k]))
            seen.add(k)
            cap = contract.get(k)
            if cap is None:
                errors.append("%s: not in Core/NativeIntroSoundKeys.cs" % k)
            elif cap + 1e-6 < float(s.get("targetSeconds", {}).get("max", 0)):
                errors.append("%s: cap %s under targetSeconds.max" % (k, cap))
            for f in ("category", "priority", "count", "generateSeconds", "targetSeconds", "prompt"):
                if f not in s:
                    errors.append("%s: missing %s" % (k, f))
            sounds.append(s)
    if errors:
        print("\n".join(errors))
        return 1
    manifest = {"schema": head["schema"], "purpose": BATCHES[a.batch][2], "file": head["file"], "generation": head["generation"],
                "edit": head["edit"], "sounds": sounds}
    text = json.dumps(manifest, indent=2, ensure_ascii=False) + "\n"
    sha = lf_sha(text.encode("utf-8"))
    pinned = pins(root, rel)
    if a.check:
        def read(p):
            return open(p, "rb").read() if os.path.exists(p) else None
        disk, copy = read(out), read(bundle)
        current = (not sounds and disk is None) or (disk is not None and disk.decode("utf-8") == text)
        as_pinned = not pinned or (disk is not None and lf_sha(disk) in pinned)
        sealed = "" if not pinned else "; sealed by %d approved job(s), %s" % (
            sum(len(v) for v in pinned.values()), "on disk as pinned" if as_pinned else "NOT AS PINNED")
        print("batch %s: %d requested keys; %s %s; bundle copy %s%s" % (
            a.batch, len(sounds), rel, "up to date" if current else "out of date: run without --check",
            "identical" if copy == disk else "DIFFERS: run without --check", sealed))
        return 0 if current and copy == disk and as_pinned else 1
    if not sounds:
        print("no rows in docs/audio/%s/*.json: nothing to compile" % BATCHES[a.batch][0])
        return 0
    if pinned and sha not in pinned:
        print("%s is sealed: approved queue jobs %s pin sha256 %s, and these rows make %s. sound_queue.py re-checks every approved job "
              "against its pin, so the builder would stop at the sound queue. Put new rows in a new batch (--batch), or restore the rows."
              % (rel, ", ".join(i for v in pinned.values() for i in v), ", ".join(sorted(pinned)), sha))
        return 1
    for p in (out, bundle):
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    n = sum(int(s["count"]) for s in sounds)
    secs = sum(float(s["generateSeconds"]) * int(s["count"]) for s in sounds)
    print("batch %s: %d keys, %d files, %.1f s of generation; %s sha256 %s" % (a.batch, len(sounds), n, secs, rel, sha))
    if a.job:
        job = {"id": a.job, "approved": True, "manifest": rel, "manifestSha256": sha,
               "keys": [s["key"] for s in sounds], "variants": sorted({v for s in sounds for v in range(1, int(s["count"]) + 1)}),
               "maxRequests": n, "maxSeconds": round(secs + 0.05, 2)}
        print(json.dumps(job, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
