<#
.SYNOPSIS
    Erzeugt für jeden Branch "release/vX.Y.Z" auf GitHub das passende Git-Tag "vX.Y.Z" und pusht alle Tags.

.DESCRIPTION
    Die Versionen dieses Repositories liegen als Branches release/v0.1.0 ... release/v1.0.0 vor.
    Dieses Skript macht daraus annotierte Tags, damit sie auf GitHub unter "Tags"/"Releases" erscheinen.

.EXAMPLE
    .\scripts\create-release-tags.ps1
#>
$ErrorActionPreference = "Stop"

git fetch origin --prune
$branches = git branch -r --list "origin/release/v*" | ForEach-Object { $_.Trim() }

foreach ($branch in $branches) {
    $version = $branch -replace "^origin/release/", ""
    if (git tag --list $version) {
        Write-Host "Tag $version existiert bereits." -ForegroundColor Yellow
        continue
    }
    git tag -a $version $branch -m "Version $($version.TrimStart('v'))"
    Write-Host "Tag $version erstellt." -ForegroundColor Green
}

git push origin --tags
Write-Host "Fertig. Die Tags sind jetzt auf GitHub sichtbar." -ForegroundColor Green
