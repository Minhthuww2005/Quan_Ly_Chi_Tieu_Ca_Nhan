param([switch]$Local, [switch]$Initialize, [switch]$NoBuild, [string]$ImportPath = '', [int]$Port = 5081, [switch]$TestProviders, [string]$AssemblyPath = '')
$ErrorActionPreference = 'Stop'
$walletRoot = Split-Path $PSScriptRoot -Parent
Set-Location $walletRoot
if ($Local) {
    $localConfig = @{}
    Get-Content '.env' | ForEach-Object { if ($_ -match '^([^#=]+)=(.*)$') { $localConfig[$matches[1]] = $matches[2] } }
    $env:ConnectionStrings__Database = 'Host=localhost;Port=55432;Database=revenueflow_demo;Username=revenueflow;Password=' + $localConfig['POSTGRES_PASSWORD']
    $env:Jwt__SigningKey = $localConfig['JWT_SIGNING_KEY']
    docker compose up -d --wait postgres
    if ($LASTEXITCODE -ne 0) { throw 'PostgreSQL local failed to start.' }
} elseif (Test-Path '.env.wallet') {
    Get-Content '.env.wallet' | ForEach-Object {
        if ($_ -match '^([^#=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim(), 'Process') }
    }
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
if ($TestProviders) {
    if (-not $Local) { throw 'TestProviders is restricted to the local QA database.' }
    $env:Google__ClientId = 'qa-google-client.apps.googleusercontent.com'
    $env:Vnpay__TmnCode = 'TESTHTD1'
    $env:Vnpay__HashSecret = 'QA_ONLY_VNPAY_SECRET_NOT_A_MERCHANT_KEY'
    $env:Vnpay__ReturnUrl = 'http://127.0.0.1:5174/payments/return'
}
$env:Database__Initialize = $Initialize.IsPresent.ToString().ToLowerInvariant()
if ($ImportPath) { $env:Database__ImportPath = (Resolve-Path -LiteralPath $ImportPath).Path } else { $env:Database__ImportPath = '' }
if (-not $NoBuild) {
    dotnet build src/WalletFunding.Api
    if ($LASTEXITCODE -ne 0) { throw 'Wallet API build failed.' }
}
$walletAssembly = Join-Path $walletRoot 'src/WalletFunding.Api/bin/Debug/net10.0/WalletFunding.Api.dll'
if ($AssemblyPath) { $walletAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path }
dotnet $walletAssembly --contentRoot (Join-Path $walletRoot 'src/WalletFunding.Api') --urls "http://localhost:$Port"
