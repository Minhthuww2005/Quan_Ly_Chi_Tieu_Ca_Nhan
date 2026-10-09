$ErrorActionPreference='Stop'
$demoBase='http://localhost:5080'
$login=Invoke-RestMethod "$demoBase/api/auth/login" -Method Post -ContentType application/json -Body (@{ email='demo1@revenueflow.local';password='Demo@123456' } | ConvertTo-Json)
$headers=@{ Authorization='Bearer '+$login.accessToken }
$context=Invoke-RestMethod "$demoBase/api/context" -Headers $headers
$date=(Get-Date).ToString('yyyy-MM-dd')
$rule=Invoke-RestMethod "$demoBase/api/recurring-rules" -Headers $headers -Method Post -ContentType application/json -Body (@{
 walletId=$context.wallets[0].id; amount=250000; note='Recurring revenue demo'; frequency='Daily'; intervalValue=1; startDate=$date; endDate=$date;timeZoneId='Asia/Ho_Chi_Minh'
} | ConvertTo-Json)
1..2 | ForEach-Object { Invoke-RestMethod "$demoBase/api/recurring-rules/$($rule.id)/trigger" -Headers $headers -Method Post | Out-Null }
for($attempt=0;$attempt -lt 20;$attempt++) {
 $runs=@(Invoke-RestMethod "$demoBase/api/recurring-rules/$($rule.id)/runs" -Headers $headers | ForEach-Object { $_ })
 if($runs.Count -gt 0) { break }
 Start-Sleep -Milliseconds 500
}
if($runs.Count -ne 1 -or $runs[0].status -ne 'Created') { throw 'Expected exactly one Created run for two triggers.' }
$result=@{ ruleId=$rule.id; triggerRequests=2; runCount=$runs.Count; status=$runs[0].status;transactionId=$runs[0].transactionId; verifiedAt=(Get-Date).ToString('o') }
New-Item -ItemType Directory -Force docs/evidence | Out-Null
$result | ConvertTo-Json | Set-Content docs/evidence/recurring-test.json -Encoding utf8
$result | Format-List
