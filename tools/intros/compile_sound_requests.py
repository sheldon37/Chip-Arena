"""compile_sound_requests.py: the intros' generated-sound requests (docs/audio/intros/README.md).

Merges every author's rows (docs/audio/intros/*.json: {"author": "...", "sounds": [<the manifest's sound entries>]}) into one
manifest on the existing schema, docs/audio/sound-requests-intros.json, and its byte-identical bundle copy under
unity/ChipArenaConversion/tools/sound_bundle/docs/audio/. It checks each key: "intro2.<intro>.<cue>", letters/digits/dots,
unique, and in Core/NativeIntroSoundKeys.cs with a cap at least its targetSeconds.max. With --job ID it also prints the approved
builder job (manifestSha256 after CRLF->LF, every key, variants, limits) to add to docs/audio/sound-queue.json and the bundle
copy. It never writes the queue itself, makes no network call and needs no key.
"""
import argparse
import glob
import hashlib
import json
import os
import re
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
ROWS = os.path.join(ROOT, "docs", "audio", "intros")
OUT = os.path.join(ROOT, "docs", "audio", "sound-requests-intros.json")
BUNDLE = os.path.join(ROOT, "unity", "ChipArenaConversion", "tools", "sound_bundle", "docs", "audio", "sound-requests-intros.json")
KEYS = os.path.join(ROOT, "unity", "ChipArenaConversion", "Assets", "ChipArena", "Core", "NativeIntroSoundKeys.cs")
TEMPLATE = os.path.join(ROOT, "docs", "audio", "sound-requests-install.json")
KEY = re.compile(r"^intro2\.[a-z0-9]+(\.[a-z0-9]+)+$")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--job")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    head = json.load(open(TEMPLATE, encoding="utf-8"))
    source = open(KEYS, encoding="utf-8").read()
    contract = {m.group(1): float(m.group(2)) for m in re.finditer(r'K\("([^"]+)",([0-9.]+)f', source)}
    sounds, seen, errors = [], set(), []
    for path in sorted(glob.glob(os.path.join(ROWS, "*.json"))):
        name = os.path.basename(path)
        rows = json.load(open(path, encoding="utf-8"))
        for s in rows.get("sounds", []):
            k = s.get("key", "")
            if not KEY.match(k):
                errors.append("%s: bad key %r" % (name, k))
            if k in seen:
                errors.append("%s: duplicate key %s" % (name, k))
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
    manifest = {
        "schema": head["schema"],
        "purpose": "Intros v2 (owner 2026-11-20: generation approved for the new intros): each intro's cue sounds. Until imported, "
                   "every cue plays its existing bank key or built-in clip (NativeIntroCue.Alt / Clip). Contract: "
                   "docs/audio/SOUND-REQUESTS.md; requests: docs/audio/intros/README.md.",
        "file": head["file"], "generation": head["generation"], "edit": head["edit"], "sounds": sounds}
    text = json.dumps(manifest, indent=2, ensure_ascii=False) + "\n"
    if a.check:
        ok = (not sounds and not os.path.exists(OUT)) or (os.path.exists(OUT) and open(OUT, encoding="utf-8").read() == text)
        print("%d requested keys; manifest %s" % (len(sounds), "up to date" if ok else "out of date: run without --check"))
        return 0 if ok else 1
    if not sounds:
        print("no rows in docs/audio/intros/*.json: nothing to compile")
        return 0
    for p in (OUT, BUNDLE):
        os.makedirs(os.path.dirname(p), exist_ok=True)
        with open(p, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    sha = hashlib.sha256(text.replace("\r\n", "\n").encode("utf-8")).hexdigest()
    n = sum(int(s["count"]) for s in sounds)
    secs = sum(float(s["generateSeconds"]) * int(s["count"]) for s in sounds)
    print("%d keys, %d files, %.1f s of generation; manifest sha256 %s" % (len(sounds), n, secs, sha))
    if a.job:
        job = {"id": a.job, "approved": True, "manifest": "docs/audio/sound-requests-intros.json", "manifestSha256": sha,
               "keys": [s["key"] for s in sounds], "variants": sorted({v for s in sounds for v in range(1, int(s["count"]) + 1)}),
               "maxRequests": n, "maxSeconds": round(secs + 0.05, 2)}
        print(json.dumps(job, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
