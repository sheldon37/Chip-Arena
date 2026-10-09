#!/usr/bin/env bash
set -euo pipefail

if [[ "${BUILDER_OS:-}" != "WINDOWS" ]]; then
  echo "Chip Arena native packaging requires the Windows builder." >&2
  exit 1
fi

# A Windows builder can expose native Windows Python or Cygwin's POSIX Python.
# Use the interpreter's path convention, not just the builder OS. In particular,
# backslash paths make POSIX pathlib infer the wrong project from __file__.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_dir="$(cd -- "$script_dir/.." && pwd)"
path_mode="$(python -c 'import os; print("-wa" if os.name == "nt" else "-ua")')"
script_path="$(cygpath "$path_mode" "$script_dir/package_cloud_build.py")"
project_path="$(cygpath "$path_mode" "$project_dir")"
player_path="$(cygpath "$path_mode" "${UNITY_PLAYER_PATH:?UBA did not provide UNITY_PLAYER_PATH}")"
exec python "$script_path" --project "$project_path" --player "$player_path"
