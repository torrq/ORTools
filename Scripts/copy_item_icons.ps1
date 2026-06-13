$ErrorActionPreference = "Stop"

$legacyBuffs = Get-Content "C:\Users\nathan\Projects\RO\4RTools-OSRO\Model\Buffs\Buff.cs" -Raw
$iconsDir = "C:\Users\nathan\Projects\RO\ORTools\ORTools.UI\Icons\Items"
New-Item -ItemType Directory -Force -Path $iconsDir | Out-Null

$legacyResx = Get-Content "C:\Users\nathan\Projects\RO\4RTools-OSRO\Properties\Resources.resx" -Raw
$legacyIconsResx = Get-Content "C:\Users\nathan\Projects\RO\4RTools-OSRO\Resources\Media\Icons.resx" -Raw

$blocks = "PotionBuffs\.AddRange", "ElementBuffs\[1\]", "FoodBuffs\.AddRange", "BoxBuffs\.AddRange", "ScrollBuffs\.AddRange", "EtcBuffs\.AddRange", "FishBuffs\.AddRange"

$seenIDs = @{}

foreach ($block in $blocks) {
    # Match the block until the closing brace
    if ($legacyBuffs -match "(?s)$block.*?\n\s*\};?") {
        $blockContent = $matches[0]
        # Match b.CreateBuff("Name", "ID", "ResourceName")
        $matches2 = [regex]::Matches($blockContent, 'CreateBuff\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)')
        foreach ($m in $matches2) {
            $displayName = $m.Groups[1].Value
            $effectID = $m.Groups[2].Value
            $resName = $m.Groups[3].Value
            
            if ($seenIDs.ContainsKey($effectID)) {
                continue
            }
            $seenIDs[$effectID] = $true

            $sourceFile = $null
            # Look in Resources.resx
            if ($legacyResx -match "<data name=""$resName"".*?>\s*<value>(.*?);") {
                $sourceFile = $matches[1].Replace("..\..\", "C:\Users\nathan\Projects\RO\4RTools-OSRO\")
            }
            # Look in Icons.resx
            elseif ($legacyIconsResx -match "<data name=""$resName"".*?>\s*<value>(.*?);") {
                $sourceFile = $matches[1].Replace("..\..\", "C:\Users\nathan\Projects\RO\4RTools-OSRO\")
            }

            if ($sourceFile) {
                # Only care if the file is an item icon (contains \item\) or etc
                if (Test-Path $sourceFile) {
                    $targetFile = "$iconsDir\$effectID.png"
                    Copy-Item $sourceFile -Destination $targetFile -Force
                    Write-Host "Copied $sourceFile -> $targetFile"
                } else {
                    Write-Host "WARNING: File not found: $sourceFile"
                }
            } else {
                Write-Host "WARNING: Resource not found: $resName"
            }
        }
    }
}
