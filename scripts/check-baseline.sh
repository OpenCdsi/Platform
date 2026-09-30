#!/usr/bin/env bash
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.

# Runs Engine.slnx and diffs per-case results against tests/baseline/engine-baseline.tsv.
# Exits non-zero on any difference. The baseline includes known conformance failures on
# purpose: during the refactor, "unchanged" is the bar, not "passing".
# Pass --update to overwrite the baseline with the current results instead.
set -euo pipefail
cd "$(dirname "$0")/.."
out="$(mktemp -d)"
trap 'rm -rf "$out"' EXIT

dotnet test Engine.slnx --logger trx --results-directory "$out/trx" >/dev/null 2>&1 || true
python3 scripts/trx2tsv.py "$out/trx" > "$out/current.tsv"

if [[ "${1:-}" == "--update" ]]; then
  cp "$out/current.tsv" tests/baseline/engine-baseline.tsv
  echo "Baseline updated ($(wc -l < "$out/current.tsv" | tr -d ' ') cases)."
  exit 0
fi

if diff tests/baseline/engine-baseline.tsv "$out/current.tsv"; then
  echo "Matches baseline ($(wc -l < "$out/current.tsv" | tr -d ' ') cases)."
else
  echo "DIFFERS from baseline." >&2
  exit 1
fi
