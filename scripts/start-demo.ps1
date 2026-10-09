param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$demoRoot = Split-Path $PSScriptRoot -Parent
Set-Location $demoRoot
if (-not (Test-Path '.env')) { throw 'Copy .env.example to .env and set POSTGRES_PASSWORD and JWT_SIGNING_KEY first.' }
$demoConfig = @{}
Get-Content '.env' | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $demoConfig[$matches[1]] = $matches[2] } }
$env:ConnectionStrings__Database = 'Host=localhost;Port=55432;Database=revenueflow_demo;Username=revenueflow;Password=' + $demoConfig['POSTGRES_PASSWORD']
$env:Jwt__SigningKey = $demoConfig['JWT_SIGNING_KEY']
$env:Redis__ConnectionString = 'localhost:6379'
$env:Database__InitializeDemo = 'true'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Worker__PollIntervalSeconds = '5'
docker compose up -d --wait postgres redis
if ($LASTEXITCODE -ne 0) { throw 'Docker dependencies failed to start.' }
if (-not $NoBuild) { dotnet build src/RevenueManagement.Api; if ($LASTEXITCODE -ne 0) { throw 'Build failed.' } }
dotnet src/RevenueManagement.Api/bin/Debug/net10.0/RevenueManagement.Api.dll --contentRoot (Join-Path $demoRoot 'src/RevenueManagement.Api') --urls http://localhost:5080
