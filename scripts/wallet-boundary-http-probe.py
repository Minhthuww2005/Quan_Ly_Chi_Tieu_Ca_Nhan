import datetime,json,urllib.request
from pathlib import Path
results=[]
for port in [5081,5082]:
    for path in ['/api/health','/api/payments/vnpay/ipn','/api/payments/vnpay/ipn/','/api/payments//vnpay/ipn','/api/payments/vnpay/ipn/extra']:
        url=f'http://127.0.0.1:{port}'+path
        if path!='/api/health':url+='?vnp_SecureHash=not-hex'
        with urllib.request.urlopen(url,timeout=15) as response:
            body=json.loads(response.read());status=response.status
            assert status==200
            assert (body.get('database')=='PostgreSQL') if path=='/api/health' else (body.get('RspCode')=='97')
            results.append({'name':f'{port} GET {path}: '+('database health 200' if path=='/api/health' else 'invalid signature rejected with RspCode 97'),'status':'passed','httpStatus':status})
target=Path(__file__).resolve().parents[1]/'docs/report-revision/boundary-http-results.json'
target.write_text(json.dumps({'generatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'passed':len(results),'scope':'two updated local API hosts; health reads and deliberately invalid IPN signatures; no valid settlement, wallet mutation or Neon access','results':results},ensure_ascii=False,indent=2),encoding='utf8')
print('PASS: 10 live HTTP checks; invalid callbacks rejected on both updated local API hosts.')
