param([string]$ResultsDirectory='docs/test-results/final')
$ErrorActionPreference='Stop'
$demoRoot=Split-Path $PSScriptRoot -Parent
Set-Location $demoRoot
$demoConfig=@{}
Get-Content '.env' | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $demoConfig[$matches[1]]=$matches[2] } }
$env:REVENUEFLOW_TEST_DATABASE='Host=localhost;Port=55432;Database=revenueflow_demo;Username=revenueflow;Password='+$demoConfig['POSTGRES_PASSWORD']
$env:REVENUEFLOW_TEST_API='http://localhost:5080'
$env:Redis__ConnectionString='localhost:6379'
Invoke-RestMethod "$env:REVENUEFLOW_TEST_API/health" | Format-List
dotnet test RevenueManagement.slnx --no-build --logger trx --results-directory $ResultsDirectory
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
