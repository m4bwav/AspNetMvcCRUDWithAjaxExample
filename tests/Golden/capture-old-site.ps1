# Golden capture of the 2014 site (package-modernize, repository variant for websites, Phase 0).
#
# Usage (Windows, PowerShell 7, as administrator; made for a throwaway GitHub-hosted windows runner):
#   pwsh tests/Golden/capture-old-site.ps1 -Old <a checkout of commit df9fb35> -Out <folder>
#
# Builds the 2014 project from the frozen commit with Visual Studio's MSBuild, hosts it under IIS Express on the port the
# project named (61036), and records every case in tests/Golden/Capture against it, twice, each time from a fresh copy
# of the committed database; the two recordings must be byte-identical. What had to change for a 2014 project to build
# and run on a 2026 machine is written to host-notes.txt and from there into the recording's header ("host.*"):
#   - the .NET Framework 4.0 reference assemblies come from the Microsoft.NETFramework.ReferenceAssemblies.net40 package;
#   - ASP.NET MVC 3, Web Pages 1.0 and Razor 1.0 (GAC installs in 2014) come from their Microsoft NuGet packages, at the
#     versions current in April 2014, copied into bin/ ("bin deployment", a supported MVC 3 setup);
#   - SQL Server Compact 4.0 SP1 is installed on the runner from Microsoft's signed x64 runtime installer,
#     as it was installed on the 2014 machine (GAC, machine.config provider, and the Entity Framework 4 provider dll
#     that the NuGet package lacks);
#   - the Visual Studio 2010 web application targets, which the project imports by a v10.0 path, are the installed
#     Visual Studio's own, copied to that path on the runner.
# Nothing in the frozen source changes.
# The recordings in tests/Golden are made once, in Phase 0, and never regenerated from new code.

param(
    [Parameter(Mandatory = $true)][string]$Old,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Port = 61036,
    [int]$Runs = 2
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$commit = 'df9fb3532b6c476ca2a9f204cc6ff8c46cea546d'
$sdfPath = 'CRUDAjaxExampleWeb/App_Data/DeveloperTest.sdf'
$sdfBlob = 'e410b978d5cf4a311ab2e53b54e261496d9ff2c6'
$packages = @(
    @{ Id = 'Microsoft.AspNet.Mvc'; Version = '3.0.20105.1'; Sha256 = 'f16caa8b70ee0cfb8e38ab280c7e1ee64d900691e73388a8e492667b081fb723' },
    @{ Id = 'Microsoft.AspNet.WebPages'; Version = '1.0.20105.408'; Sha256 = '5f6fe506c94eb2445cfa698cf98eaff1a92f4c2b089a556decf5eefbda437b81' },
    @{ Id = 'Microsoft.AspNet.Razor'; Version = '1.0.20105.408'; Sha256 = '05c25cc42155a8b5b040ae84e40d8fd1577c44a6a684c943f40cf058381ad3ab' },
    @{ Id = 'Microsoft.Web.Infrastructure'; Version = '1.0.0'; Sha256 = '94d5011a05bd4c22cf4bdbbd871650de632c1adf0ccd210e9d7b3bd67f1c98b0' },
    @{ Id = 'Microsoft.NETFramework.ReferenceAssemblies.net40'; Version = '1.0.3'; Sha256 = '54d6e20a1b61caf79395d6d71d091265e81f5a5705ac4ae52af45ca143c3c694' }
)

# The x86 package refuses a 64-bit Windows in its launch conditions ("not supported on x64 Operating System", run
# 36724486565); the x64 package is the one for a 64-bit machine.
$sqlce = @(
    @{ Arch = 'x64'; Sha256 = '443d149c7a1a3d5c26189bf727a55287341fad620225fd7aed610ba1762286e4' }
)
$sqlceUrl = 'https://download.microsoft.com/download/f/f/d/ffdf76e3-9e55-41da-a750-1798b971936c/ENU/SSCERuntime_{0}-ENU.exe'

$here = $PSScriptRoot
$Old = (Resolve-Path $Old).Path
New-Item -ItemType Directory -Force -Path $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$work = Join-Path $Out 'work'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$notes = Join-Path $Out 'host-notes.txt'
Set-Content -Path $notes -Value @() -Encoding utf8NoBOM

function Note([string]$key, [string]$value) {
    Add-Content -Path $notes -Value ("{0}: {1}" -f $key, $value) -Encoding utf8NoBOM
    Write-Host ("{0}: {1}" -f $key, $value)
}

function Step([string]$text) {
    Write-Host ''
    Write-Host "== $text"
}

Step 'the frozen commit'
$head = (git -C $Old rev-parse HEAD).Trim()
if ($head -ne $commit) { throw "the checkout is at $head, not the frozen commit $commit" }
if ((git -C $Old status --porcelain) -ne $null) { throw 'the checkout of the frozen commit has changes' }
Note 'commit' $commit
Note 'checkout.eol' ((git -C $Old ls-files --eol CRUDAjaxExampleWeb/Views/Home/Index.cshtml) -replace '\s+', ' ')

Step 'packages from nuget.org, checked against their SHA-256'
$lib = @{}
foreach ($p in $packages) {
    $id = $p.Id.ToLowerInvariant()
    $file = Join-Path $work "$id.$($p.Version).zip"
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$($p.Version)/$id.$($p.Version).nupkg" -OutFile $file
    $hash = (Get-FileHash -Algorithm SHA256 $file).Hash.ToLowerInvariant()
    if ($hash -ne $p.Sha256) { throw "$($p.Id) $($p.Version): SHA-256 $hash, expected $($p.Sha256)" }
    $dir = Join-Path $work "$id.$($p.Version)"
    Expand-Archive -Path $file -DestinationPath $dir -Force
    $lib[$p.Id] = $dir
    Note "package.$($p.Id)" "$($p.Version) sha256 $hash"
}

Step 'SQL Server Compact 4.0 SP1 runtime (x64), from Microsoft'
foreach ($r in $sqlce) {
    $exe = Join-Path $work "SSCERuntime_$($r.Arch)-ENU.exe"
    Invoke-WebRequest -Uri ($sqlceUrl -f $r.Arch) -OutFile $exe
    $hash = (Get-FileHash -Algorithm SHA256 $exe).Hash.ToLowerInvariant()
    if ($hash -ne $r.Sha256) { throw "SSCERuntime_$($r.Arch): SHA-256 $hash, expected $($r.Sha256)" }
    $sig = Get-AuthenticodeSignature $exe
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike 'CN=Microsoft Corporation*') { throw "SSCERuntime_$($r.Arch): signature $($sig.Status) $($sig.SignerCertificate.Subject)" }
    $dir = Join-Path $work "sqlce-$($r.Arch)"
    & 7z x $exe "-o$dir" -y | Out-Null
    $msi = Get-ChildItem $dir -Recurse -Filter *.msi | Select-Object -First 1
    if (-not $msi) { Get-ChildItem $dir -Recurse | Out-String | Write-Host; throw "no msi inside SSCERuntime_$($r.Arch)" }
    $p = Start-Process msiexec.exe -ArgumentList @('/i', $msi.FullName, '/quiet', '/norestart', '/l*v', (Join-Path $Out "sqlce-$($r.Arch).log")) -Wait -PassThru
    if ($p.ExitCode -ne 0) { throw "SQL Server Compact $($r.Arch) install exited $($p.ExitCode)" }
    Note "sqlce.$($r.Arch)" "SSCERuntime_$($r.Arch)-ENU.exe sha256 $hash, signed by Microsoft, installed from $($msi.Name)"
}
$gac = Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL') -Directory | Where-Object { $_.Name -like 'System.Data.SqlServerCe*' } | ForEach-Object { $_.Name + ' ' + ((Get-ChildItem $_.FullName -Directory | Select-Object -ExpandProperty Name) -join ',') }
if (-not ($gac -match 'System.Data.SqlServerCe.Entity')) { throw "no System.Data.SqlServerCe.Entity in the GAC: $gac" }
Note 'sqlce.gac' ($gac -join '; ')

Step 'Visual Studio, MSBuild and the web application targets'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$vs = (& $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath).Trim()
$msbuild = Join-Path $vs 'MSBuild/Current/Bin/MSBuild.exe'
if (-not (Test-Path $msbuild)) { throw "no MSBuild at $msbuild" }
$found = Get-ChildItem (Join-Path $vs 'MSBuild/Microsoft/VisualStudio') -Directory -Filter 'v*' |
    Where-Object { $_.Name -ne 'v10.0' -and (Test-Path (Join-Path $_.FullName 'WebApplications/Microsoft.WebApplication.targets')) } |
    Sort-Object { [version]($_.Name.TrimStart('v')) } | Select-Object -Last 1
if (-not $found) {
    Get-ChildItem (Join-Path $vs 'MSBuild/Microsoft/VisualStudio') | Format-Table -AutoSize | Out-String | Write-Host
    throw "no Microsoft.WebApplication.targets under $vs"
}
$targetsNew = Join-Path $found.FullName 'WebApplications'
$targets10 = Join-Path ${env:ProgramFiles(x86)} 'MSBuild/Microsoft/VisualStudio/v10.0/WebApplications'
# The project imports $(MSBuildExtensionsPath32)/Microsoft/VisualStudio/v10.0/...; with Visual Studio's MSBuild that
# property is the Visual Studio MSBuild folder, so the copy goes there (and to Program Files (x86) for older layouts).
foreach ($dest in @((Join-Path $vs 'MSBuild/Microsoft/VisualStudio/v10.0/WebApplications'), $targets10)) {
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    Copy-Item -Path (Join-Path $targetsNew '*') -Destination $dest -Recurse -Force
}
Note 'visualStudio' ((& $vswhere -latest -products * -property catalog_productDisplayVersion).Trim())
Note 'msbuild' ((& $msbuild -version -nologo | Select-Object -Last 1).Trim())
Note 'adaptation.webTargets' ("Visual Studio's $($found.Name) WebApplications targets copied to the v10.0 path the project imports")

Step 'build the 2014 project (Debug, as it was run from Visual Web Developer 2010)'
$refs = @(
    (Join-Path $lib['Microsoft.AspNet.Mvc'] 'lib/net40'),
    (Join-Path $lib['Microsoft.AspNet.WebPages'] 'lib/net40'),
    (Join-Path $lib['Microsoft.AspNet.Razor'] 'lib/net40'),
    (Join-Path $lib['Microsoft.Web.Infrastructure'] 'lib/net40')
) -join ';'
$refRoot = Join-Path $lib['Microsoft.NETFramework.ReferenceAssemblies.net40'] 'build'
$project = Join-Path $Old 'CRUDAjaxExampleWeb/CRUDAjaxExampleWeb.csproj'
& $msbuild $project /nologo /v:m /p:Configuration=Debug "/p:TargetFrameworkRootPath=$refRoot" "/p:ReferencePath=$refs" | Tee-Object -FilePath (Join-Path $Out 'build.log')
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit $LASTEXITCODE" }
Note 'adaptation.referenceAssemblies' 'net40 reference assemblies from Microsoft.NETFramework.ReferenceAssemblies.net40 (TargetFrameworkRootPath)'
Note 'build' 'MSBuild, Configuration Debug, TargetFrameworkVersion v4.0 as the project says'

$site = Join-Path $Old 'CRUDAjaxExampleWeb'
$bin = Join-Path $site 'bin'
$dll = Join-Path $bin 'CRUDAjaxExampleWeb.dll'
$text = [System.Text.Encoding]::ASCII.GetString([System.IO.File]::ReadAllBytes($dll))
foreach ($r in @('PersonModel.csdl', 'PersonModel.ssdl', 'PersonModel.msl')) {
    if (-not $text.Contains($r)) { throw "the built assembly has no $r resource: EntityDeploy did not run" }
}
Note 'check.edmxResources' 'PersonModel.csdl, .ssdl and .msl embedded'

Step 'bin deployment of MVC 3, Web Pages and Razor'
foreach ($id in @('Microsoft.AspNet.Mvc', 'Microsoft.AspNet.WebPages', 'Microsoft.AspNet.Razor', 'Microsoft.Web.Infrastructure')) {
    Get-ChildItem (Join-Path $lib[$id] 'lib/net40') -Filter *.dll | ForEach-Object {
        $target = Join-Path $bin $_.Name
        if (-not (Test-Path $target)) { Copy-Item $_.FullName $target }
    }
}
Note 'adaptation.binDeploy' ((Get-ChildItem $bin -Filter *.dll | Select-Object -ExpandProperty Name | Sort-Object) -join ', ')

Step 'IIS Express and the machine'
$iis = @((Join-Path $env:ProgramFiles 'IIS Express/iisexpress.exe'), (Join-Path ${env:ProgramFiles(x86)} 'IIS Express/iisexpress.exe')) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iis) { throw 'no IIS Express in Program Files or Program Files (x86)' }
Note 'server' ("IIS Express " + (Get-Item $iis).VersionInfo.FileVersion + " (" + $iis + "), /clr:v4.0, port " + $Port)
Note 'image' ("$env:ImageOS $env:ImageVersion")
Note 'os' ((Get-CimInstance Win32_OperatingSystem | ForEach-Object { $_.Caption + ' ' + $_.Version }))
Note 'netFramework' ('release ' + (Get-ItemProperty 'HKLM:/SOFTWARE/Microsoft/NET Framework Setup/NDP/v4/Full').Release)
Note 'culture' ((Get-Culture).Name + ', UI ' + (Get-UICulture).Name + ', system locale ' + (Get-WinSystemLocale).Name)
Note 'timeZone' ((Get-TimeZone).Id)

Step 'the capture program'
dotnet build (Join-Path $here 'Capture/Capture.csproj') -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'the capture program did not build' }
$capture = Join-Path $here 'Capture/bin/Release/net10.0/Capture.dll'

for ($run = 1; $run -le $Runs; $run++) {
    Step "run $run"
    git -C $Old checkout -- $sdfPath
    $blob = (git -C $Old hash-object $sdfPath).Trim()
    if ($blob -ne $sdfBlob) { throw "the database is blob $blob, not the committed $sdfBlob" }
    $log = Join-Path $Out "iisexpress-$run.log"
    $server = Start-Process -FilePath $iis -ArgumentList @("/path:$site", "/port:$Port", '/clr:v4.0', '/systray:false') -PassThru -NoNewWindow -RedirectStandardOutput $log -RedirectStandardError "$log.err"
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $ready; $i++) {
        Start-Sleep -Seconds 1
        try { $ready = (Invoke-WebRequest -Uri "http://localhost:$Port/Content/Site.css" -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200 } catch { $ready = $false }
    }
    if (-not $ready) { Stop-Process -Id $server.Id -Force; throw "IIS Express did not answer on port $Port" }
    $pages = @()
    if ($run -eq 1) { $pages = @((Join-Path $Out 'pages')) }
    dotnet $capture "http://localhost:$Port" (Join-Path $Out "run$run.json") $notes @pages
    $code = $LASTEXITCODE
    Stop-Process -Id $server.Id -Force
    $server.WaitForExit()
    if ($code -ne 0) { throw "the capture exited $code on run $run" }
}

Step 'compare the runs'
$first = (Get-FileHash -Algorithm SHA256 (Join-Path $Out 'run1.json')).Hash.ToLowerInvariant()
for ($run = 2; $run -le $Runs; $run++) {
    $h = (Get-FileHash -Algorithm SHA256 (Join-Path $Out "run$run.json")).Hash.ToLowerInvariant()
    if ($h -ne $first) { throw "run $run differs from run 1 ($h, $first)" }
}
Copy-Item (Join-Path $Out 'run1.json') (Join-Path $Out '1.0.0.iisexpress-windows.json')
Write-Host "IDENTICAL over $Runs runs: sha256 $first"
Set-Content -Path (Join-Path $Out 'SHA256SUMS') -Value "$first  1.0.0.iisexpress-windows.json" -Encoding utf8NoBOM
Remove-Item -Recurse -Force $work
