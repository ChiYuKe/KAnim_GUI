param(
    [Parameter(Mandatory = $true)]
    [string]$CoverageFile,
    [double]$MinimumLineRate = 0.80
)

[xml]$coverage = Get-Content -LiteralPath $CoverageFile
$packages = @($coverage.coverage.packages.package | Where-Object {
    $_.name -in @("KAnimGui.Core", "KAnimGui.Application")
})

if ($packages.Count -ne 2) {
    throw "Coverage file must contain KAnimGui.Core and KAnimGui.Application."
}

$failed = @()
foreach ($package in $packages) {
    $rate = [double]$package.'line-rate'
    Write-Host ("{0}: {1:P2}" -f $package.name, $rate)
    if ($rate -lt $MinimumLineRate) {
        $failed += "{0}={1:P2}" -f $package.name, $rate
    }
}

if ($failed.Count -gt 0) {
    $threshold = "{0:P0}" -f $MinimumLineRate
    throw "Line coverage is below ${threshold}: $($failed -join ', ')"
}
