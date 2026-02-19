# PowerShell script to build MSIX bundle for all platforms

Write-Host "Building MSIX Bundle for Microsoft Store submission..." -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green

# Read version from Package.appxmanifest
$manifestPath = "PingTool.WinUI3\Package.appxmanifest"
[xml]$manifest = Get-Content $manifestPath
$version = $manifest.Package.Identity.Version
Write-Host "Detected version from manifest: $version" -ForegroundColor Cyan

# Set the configuration
$Configuration = "Release"
$ProjectPath = "PingTool.WinUI3\PingTool.WinUI3.csproj"
$Platforms = @("x86", "x64", "ARM64")

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Cyan
Remove-Item -Path "PingTool.WinUI3\bin\Package" -Recurse -Force -ErrorAction SilentlyContinue

# Build and package for each platform
foreach ($Platform in $Platforms) {
    Write-Host "`nBuilding and packaging for $Platform..." -ForegroundColor Cyan
    
    # Build the project
    msbuild $ProjectPath `
        /p:Configuration=$Configuration `
        /p:Platform=$Platform `
        /p:AppxBundle=Never `
        /p:AppxPackageDir="bin\Package\" `
        /p:GenerateAppxPackageOnBuild=true `
        /p:AppxPackageSigningEnabled=false `
        /t:Restore,Build,_GenerateAppxPackage `
        /m `
        /verbosity:minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to build for $Platform" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "Successfully built $Platform package" -ForegroundColor Green
}

# Create the bundle from individual packages using MakeAppx
Write-Host "`n================================================" -ForegroundColor Cyan
Write-Host "Creating MSIX Bundle from all platforms..." -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan

# Find MakeAppx.exe (x64 version)
$makeAppxPath = $null

# Try Windows SDK first
$windowsKitPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
)

foreach ($path in $windowsKitPaths) {
    if (Test-Path $path) {
        $makeAppxPath = $path
        break
    }
}

# If not found, search in SDK BuildTools (x64 version only)
if (-not $makeAppxPath) {
    $sdkPath = Get-ChildItem -Path "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools" -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($sdkPath) {
        $makeAppxPath = Get-ChildItem -Path $sdkPath.FullName -Filter "makeappx.exe" -Recurse | Where-Object { $_.FullName -like "*\x64\*" } | Select-Object -First 1 | ForEach-Object { $_.FullName }
    }
}

if ($makeAppxPath -and (Test-Path $makeAppxPath)) {
    Write-Host "Using MakeAppx.exe: $makeAppxPath" -ForegroundColor Cyan
    
    # Create bundle mapping file
    $bundleMapPath = "PingTool.WinUI3\bin\Package\BundleMap.txt"
    $packageDir = "PingTool.WinUI3\bin\Package"
    
    $bundleContent = @()
    $bundleContent += "[Files]"
    
    # Find all MSIX packages
    $msixFiles = Get-ChildItem -Path $packageDir -Filter "*.msix" -Recurse | Where-Object { $_.Name -notlike "*.msixsym" }
    foreach ($msixFile in $msixFiles) {
        $bundleContent += """$($msixFile.FullName)"" ""$($msixFile.Name)"""
        Write-Host "  Adding to bundle: $($msixFile.Name)" -ForegroundColor DarkGray
    }
    
    $bundleContent | Out-File -FilePath $bundleMapPath -Encoding UTF8

    # Create the bundle using version from manifest
    $bundleName = "PingTool.WinUI3_${version}_x86_x64_ARM64.msixbundle"
    $bundlePath = "$packageDir\$bundleName"

    Write-Host "`nCreating bundle with version $version..." -ForegroundColor Cyan
    & $makeAppxPath bundle /f $bundleMapPath /p $bundlePath /bv $version /o
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n================================================" -ForegroundColor Green
        Write-Host "MSIX Bundle created successfully!" -ForegroundColor Green
        Write-Host "================================================" -ForegroundColor Green
        Write-Host "`nBundle location:" -ForegroundColor Cyan
        Write-Host "  $bundlePath" -ForegroundColor Yellow
        
        if (Test-Path $bundlePath) {
            $size = [math]::Round((Get-Item $bundlePath).Length / 1MB, 2)
            Write-Host "`nBundle size: $size MB" -ForegroundColor Cyan
        }
        
        Write-Host "`nUpload this .msixbundle file to Microsoft Store Partner Center" -ForegroundColor Green
    } else {
        Write-Host "`nFailed to create bundle with MakeAppx" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "ERROR: MakeAppx.exe not found!" -ForegroundColor Red
    Write-Host "Please install Windows SDK or ensure Microsoft.Windows.SDK.BuildTools is properly installed" -ForegroundColor Yellow
    exit 1
}
