#!/usr/bin/env bash
# This Source Code Form is subject to the terms of the Mozilla Public
# License, v. 2.0. If a copy of the MPL was not distributed with this
# file, You can obtain one at https://mozilla.org/MPL/2.0/.
#
# Tags origin/main directly, not the local main branch - `git fetch` alone never moves local
# main forward (that needs a separate pull/merge/reset), so tagging local HEAD right after a
# fetch can still grab a stale commit. This sidesteps that class of mistake entirely by never
# touching or relying on local main's position, only the just-fetched origin/main.
#
# Pushing the tag triggers .github/workflows/build-android.yml's release job: builds Android +
# Windows (x64/arm64), then attaches all three to a GitHub Release for this tag.
set -euo pipefail

if [ $# -ne 1 ]; then
    echo "Usage: $0 vX.Y.Z" >&2
    exit 1
fi

TAG="$1"

if [[ ! "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+([-+].+)?$ ]]; then
    echo "Error: '$TAG' doesn't look like vMAJOR.MINOR.PATCH (e.g. v1.2.3)" >&2
    exit 1
fi

echo "Fetching origin/main..."
git fetch origin main

TARGET_SHA="$(git rev-parse --short origin/main)"
echo "Tagging $TAG at origin/main ($TARGET_SHA)..."
git tag "$TAG" origin/main

echo "Pushing $TAG..."
git push origin "$TAG"

echo
echo "Done. $TAG is at $TARGET_SHA - watch the release build at:"
echo "  https://github.com/OpenCdsi/OpenCdsi.Mobile/actions"
