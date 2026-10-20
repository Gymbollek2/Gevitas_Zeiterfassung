#!/usr/bin/env bash
# Erzeugt für jeden Branch "release/vX.Y.Z" das passende Git-Tag "vX.Y.Z" und pusht alle Tags.
set -euo pipefail

git fetch origin --prune
for branch in $(git branch -r --list 'origin/release/v*'); do
    version="${branch#origin/release/}"
    if git rev-parse -q --verify "refs/tags/$version" >/dev/null; then
        echo "Tag $version existiert bereits."
        continue
    fi
    git tag -a "$version" "$branch" -m "Version ${version#v}"
    echo "Tag $version erstellt."
done

git push origin --tags
echo "Fertig. Die Tags sind jetzt auf GitHub sichtbar."
