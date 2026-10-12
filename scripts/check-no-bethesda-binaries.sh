#!/usr/bin/env bash
# Fail if Bethesda compiler artifacts are committed anywhere in the tree.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

violations="$(
  git ls-files -z |
    tr '\0' '\n' |
    grep -E '(\.pex$|(^|/)PCompiler\.dll$|(^|/)Antlr3[^/]*\.dll$)' || true
)"

if [[ -n "${violations}" ]]; then
  echo "error: Bethesda binaries must not be committed" >&2
  echo "${violations}" >&2
  exit 1
fi

echo "OK: no committed *.pex / PCompiler.dll / Antlr3*.dll"
