"""Compile Runtime and Editor against the project's pinned Unity assemblies.
No Unity process, license activation, project import or player build is performed.
This is a semantic compile gate, not a replacement for native build/play checks.
"""
import argparse
from pathlib import Path
import re
import shutil
import subprocess
import tarfile
import urllib.request

PROJECT = Path(__file__).resolve().parents[1]


def project_version():
    text = (PROJECT / "ProjectSettings/ProjectVersion.txt").read_text()
    match = re.search(r"m_EditorVersionWithRevision: ([0-9]+\.[0-9]+\.[0-9]+[abfp][0-9]+) \(([0-9a-f]+)\)", text)
    if not match:
        raise RuntimeError("Pinned Unity version and revision are required")
    return match.groups()


def download_references(destination):
    version, revision = project_version()
    # Unity's versioned Linux Editor archive; never use a floating latest URL.
    url = f"https://download.unity3d.com/download_unity/{revision}/LinuxEditorInstaller/Unity-{version}.tar.xz"
    print(f"Fetching Unity {version} reference assemblies from {url}", flush=True)
    destination.mkdir(parents=True, exist_ok=True)
    count = 0
    with urllib.request.urlopen(url, timeout=120) as response:
        with tarfile.open(fileobj=response, mode="r|xz") as archive:
            for member in archive:
                parts = Path(member.name).parts
                if ".." in parts or Path(member.name).is_absolute() or not member.isfile():
                    continue
                name = member.name
                if not name.endswith(".dll") or not ("/Data/Managed/" in "/" + name or "/Data/NetStandard/" in "/" + name):
                    continue
                target = destination / name
                target.parent.mkdir(parents=True, exist_ok=True)
                source = archive.extractfile(member)
                if source is None:
                    raise RuntimeError(f"Cannot read reference: {name}")
                with source, target.open("wb") as output:
                    shutil.copyfileobj(source, output)
                count += 1
                if count % 100 == 0:
                    print(f"Extracted {count} reference files", flush=True)
    if not count:
        raise RuntimeError("Unity archive contained no reference assemblies")
    print(f"Extracted {count} reference files", flush=True)


def reference_set(root):
    engines = list(root.glob("**/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll"))
    if len(engines) != 1:
        raise RuntimeError(f"Expected one UnityEngine.CoreModule.dll, found {len(engines)}")
    # Unity installations place UnityEngine modules under Data/Managed/UnityEngine.
    managed = engines[0].parent
    while managed.name != "Managed" and managed != managed.parent:
        managed = managed.parent
    if managed.name != "Managed":
        raise RuntimeError("Unity Managed directory was not found")
    standards = list((managed.parent / "NetStandard" / "ref").rglob("netstandard.dll"))
    if len(standards) != 1:
        raise RuntimeError("Expected the Unity NetStandard reference profile")
    refs = {p.name: p for p in standards[0].parent.glob("*.dll") if not p.name.startswith("Unity")}
    unity_refs = list(managed.glob("Unity*.dll"))
    unity_refs += list((managed / "UnityEngine").glob("Unity*.dll"))
    unity_refs += list((managed / "UnityEditor").glob("Unity*.dll"))
    for p in sorted(unity_refs):
        if p.name in refs and refs[p.name] != p:
            raise RuntimeError(f"Ambiguous Unity assembly: {p.name}")
        refs[p.name] = p
    if "UnityEditor.CoreModule.dll" not in refs:
        raise RuntimeError("Unity Editor reference assemblies are required")
    return list(refs.values())


def compiler(dotnet):
    lines = subprocess.check_output([dotnet, "--list-sdks"], text=True).splitlines()
    for line in reversed(lines):
        match = re.fullmatch(r"(\S+) \[(.+)\]", line.strip())
        if match:
            candidate = Path(match[2]) / match[1] / "Roslyn/bincore/csc.dll"
            if candidate.is_file():
                return candidate
    raise RuntimeError("A .NET SDK with Roslyn is required")


def compile_assembly(dotnet, csc, output, name, sources, refs, defines):
    if not sources:
        raise RuntimeError(f"No sources found for {name}")
    dll = output / (name + ".dll")
    args = ["-nologo", "-target:library", "-nostdlib+", "-optimize+", "-langversion:latest",
            "-define:" + ";".join(defines), "-out:" + str(dll)]
    args += ["-r:" + str(p) for p in refs] + [str(p) for p in sources]
    # Response files keep large source/reference lists below OS argument limits.
    rsp = output / (name + ".rsp")
    rsp.write_text("\n".join('"' + arg.replace('"', '\\"') + '"' for arg in args) + "\n")
    result = subprocess.run([dotnet, str(csc), "@" + str(rsp)], text=True,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    (output / (name + ".log")).write_text(result.stdout)
    print(result.stdout, end="", flush=True)
    result.check_returncode()
    print(f"PASS Unity assembly compile: {name}", flush=True)
    return dll


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-root", type=Path)
    parser.add_argument("--download", action="store_true")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    args = parser.parse_args()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    if args.download == (args.unity_root is not None):
        parser.error("Choose --download or --unity-root")
    root = args.unity_root
    if args.download:
        root = args.output / "unity-references"
        if root.exists():
            shutil.rmtree(root)
        download_references(root)
    refs = reference_set(root.resolve())
    csc = compiler(args.dotnet)
    source = PROJECT / "Assets/ChipArena"
    runtime = sorted((source / "Core").rglob("*.cs")) + sorted((source / "Runtime").rglob("*.cs"))
    editor = sorted((source / "Editor").rglob("*.cs"))
    defines = ["UNITY_6000_3_OR_NEWER", "UNITY_6000_0_OR_NEWER", "UNITY_STANDALONE", "UNITY_STANDALONE_WIN", "UNITY_64"]
    player_refs = [p for p in refs if not p.name.startswith("UnityEditor")]
    compile_assembly(args.dotnet, csc, args.output, "ChipArena.Player", runtime, player_refs, defines)
    compiled = compile_assembly(args.dotnet, csc, args.output, "Assembly-CSharp", runtime, refs, defines + ["UNITY_EDITOR", "UNITY_EDITOR_WIN"])
    compile_assembly(args.dotnet, csc, args.output, "Assembly-CSharp-Editor", editor, refs + [compiled], defines + ["UNITY_EDITOR", "UNITY_EDITOR_WIN"])


if __name__ == "__main__":
    main()
