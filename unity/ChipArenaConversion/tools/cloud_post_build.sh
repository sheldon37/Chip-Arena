#!/usr/bin/env bash
set -euo pipefail

if [[ "${BUILDER_OS:-}" != "WINDOWS" ]]; then
  echo "Chip Arena native packaging requires the Windows builder." >&2
  exit 1
fi

# UBA invokes bash on Windows. Python needs native paths, including for spaces.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
python "$(cygpath -wa "$script_dir/package_cloud_build.py")" \
  --player "$(cygpath -wa "${UNITY_PLAYER_PATH:?UBA did not provide UNITY_PLAYER_PATH}")"
