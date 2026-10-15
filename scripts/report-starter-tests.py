import json,urllib.request,urllib.error,datetime
from pathlib import Path
observations=[]
def request(path,body=None):
    payload=None if body is None else body.encode()
    req=urllib.request.Request('http://127.0.0.1:5097'+path,data=payload,headers={'Content-Type':'application/json'})
    try:
        with urllib.request.urlopen(req,timeout=10) as response:status=response.status;text=response.read().decode()
    except urllib.error.HTTPError as response:status=response.code;text=response.read().decode()
    observations.append({'path':path,'input':body,'status':status,'body':text})
    return status,json.loads(text) if text else None
first=request('/scope')[1];second=request('/scope')[1]
assert first['sameScope'] and second['sameScope']
assert first['scoped']!=second['scoped']
assert first['transient']!=first['secondTransient']
assert first['singleton']==second['singleton']
for body,expected in [('{"amount":10}',200),('{"amount":0}',400),('{"amount":-1}',400),('null',400),('{bad',400)]:
    assert request('/amount',body)[0]==expected
output=Path(__file__).resolve().parents[1]/'docs/report-revision/starter-results.json'
output.write_text(json.dumps({'generatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'requests':len(observations),'passedAssertions':9,'scope':'compiled report starter code, real HTTP loopback; two scope requests and five body cases; no database','observations':observations},ensure_ascii=False,indent=2),encoding='utf8')
print('PASS: 9 starter assertions across 7 HTTP requests.')
