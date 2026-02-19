param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$true)]
    [string]$ManifestPath
)

Write-Host "Updating manifest version to: $Version"
Write-Host "Manifest path: $ManifestPath"

try {
    # Load XML properly
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
}
catch {
    Write-Error "Failed to update manifest: $_"
    exit 1
}
