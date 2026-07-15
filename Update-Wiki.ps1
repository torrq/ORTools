$ErrorActionPreference = "Stop"

$SourceDir = "$PSScriptRoot\docs"
$WikiDir = "C:\Users\nathan\Projects\RO\ORTools.wiki"

Write-Host "Starting Wiki Update..." -ForegroundColor Cyan

if (-not (Test-Path $WikiDir)) {
    Write-Error "Wiki directory not found at $WikiDir. Please check the path."
    exit 1
}

if (-not (Test-Path $SourceDir)) {
    Write-Error "Docs directory not found at $SourceDir."
    exit 1
}

# 1. Clean the wiki directory (keep .git and _Sidebar.md so we don't overwrite manual edits)
Write-Host "Cleaning old wiki files..."
Get-ChildItem -Path $WikiDir -Exclude @(".git", "_Sidebar.md") | Remove-Item -Recurse -Force

# 2. Copy docs contents to wiki
Write-Host "Copying files from docs to wiki..."
Copy-Item -Path "$SourceDir\*" -Destination $WikiDir -Recurse -Force

# 3. Flatten the Wiki Structure (GitHub Wiki prefers all pages in root)
Write-Host "Flattening folder structure for GitHub Wiki..."
$TabsDir = Join-Path $WikiDir "tabs"
if (Test-Path $TabsDir) {
    Get-ChildItem -Path $TabsDir -Filter "*.md" | ForEach-Object {
        Move-Item -Path $_.FullName -Destination $WikiDir -Force
    }
    Remove-Item -Path $TabsDir -Recurse -Force
}

# 4. Rename README.md to Home.md
$ReadmePath = Join-Path $WikiDir "README.md"
$HomePath = Join-Path $WikiDir "Home.md"
if (Test-Path $ReadmePath) {
    Write-Host "Renaming README.md to Home.md..."
    Move-Item -Path $ReadmePath -Destination $HomePath -Force
}


# 5. Fix Markdown Links for GitHub Wiki
Write-Host "Converting Markdown links to Wiki format..."
Get-ChildItem -Path $WikiDir -Filter "*.md" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    
    # 5a. Remove 'tabs/' folder from links: [Link](tabs/page.md) -> [Link](page.md)
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '\]\(tabs/([^)]+)\.md\)', ']($1.md)')
    
    # 5b. Remove '.md' from wiki page links: [Link](page.md) -> [Link](page)
    # (We use negative lookahead to ensure we don't accidentally match image files if they somehow end in .md, though unlikely)
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '\]\((?!images/)([^)]+)\.md\)', ']($1)')
    
    # 5c. Fix image paths since we flattened the tabs folder: [Image](../images/pic.png) -> [Image](images/pic.png)
    $content = [System.Text.RegularExpressions.Regex]::Replace($content, '\]\(\.\./images/([^)]+)\)', '](images/$1)')

    Set-Content -Path $_.FullName -Value $content -NoNewline
}

# 6. Git Commit and Push
Write-Host "Committing to Git..."
Set-Location -Path $WikiDir

git add .
$status = git status --porcelain

if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "No changes detected. The Wiki is already up to date!" -ForegroundColor Yellow
} else {
    git commit -m "Auto-update wiki from docs folder (flattened & fixed links)"
    git push
    Write-Host "Wiki successfully updated and pushed to GitHub!" -ForegroundColor Green
}

# Return to original directory
Set-Location -Path $PSScriptRoot
