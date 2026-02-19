param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$true)]
    [string]$ManifestPath,

    [Parameter(Mandatory=$false)]
    [string]$InnoSetupPath = "InnoSetup.iss"
)

Write-Host "Updating manifest version to: $Version"
Write-Host "Manifest path: $ManifestPath"

try {
    # Update Package.appxmanifest
    [xml]$manifest = Get-Content $ManifestPath

    # Update the version attribute
    $manifest.Package.Identity.Version = $Version

    # Save with proper encoding (UTF-8 without BOM)
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    $writer = New-Object System.IO.StreamWriter($ManifestPath, $false, $utf8NoBom)
    $manifest.Save($writer)
    $writer.Close()

    Write-Host "✓ Manifest updated successfully to version: $Version"

    # Verify the update
    [xml]$updatedManifest = Get-Content $ManifestPath
    $actualVersion = $updatedManifest.Package.Identity.Version
    Write-Host "✓ Verified version in manifest: $actualVersion"

    if ($actualVersion -ne $Version) {
        Write-Error "Version mismatch! Expected: $Version, Got: $actualVersion"
        exit 1
    }

    # Update InnoSetup.iss if it exists
    if (Test-Path $InnoSetupPath) {
        Write-Host "Updating InnoSetup script: $InnoSetupPath"
        $innoContent = Get-Content $InnoSetupPath -Raw
        $innoContent = $innoContent -replace '#define MyAppVersion "[\d\.]+"', "#define MyAppVersion `"$Version`""
        $innoContent | Set-Content $InnoSetupPath -Encoding UTF8
        Write-Host "✓ InnoSetup script updated successfully"

        # Verify InnoSetup update
        $verifyLine = Get-Content $InnoSetupPath | Select-String "MyAppVersion"
        Write-Host "✓ Verified InnoSetup version: $verifyLine"
    }
    else {
        Write-Host "⚠️ InnoSetup script not found at: $InnoSetupPath (skipping)"
    }
}
catch {
    Write-Error "Failed to update version: $_"
    exit 1
}
