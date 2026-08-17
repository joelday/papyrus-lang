#!/usr/bin/env bash
# Fail if committed Bethesda compiler artifacts appear outside the grandfathered tree.
# Grandfathered: dependencies/compilers/  (do not add new files there)
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

grandfathered='dependencies/compilers/'

violations="$(
  git ls-files -z |
    tr '\0' '\n' |
    grep -E '(\.pex$|(^|/)PCompiler\.dll$|(^|/)Antlr3[^/]*\.dll$)' |
    grep -v "^${grandfathered}" || true
)"

if [[ -n "${violations}" ]]; then
  echo "error: Bethesda binaries must not be committed outside ${grandfathered}" >&2
  echo "${violations}" >&2
  exit 1
fi

echo "OK: no committed *.pex / PCompiler.dll / Antlr3*.dll outside ${grandfathered}"
