"""Records the behavior of two real loopback servers, not speed rankings."""
import json,urllib.request,urllib.error,datetime,pathlib
cases=[('Hợp lệ',b'{"amount":10}'),('Bằng không',b'{"amount":0}'),('Số âm',b'{"amount":-1}'),('Thiếu trường',b'{}'),('Chuỗi chữ',b'{"amount":"abc"}'),('Chuỗi số',b'{"amount":"10"}'),('Số thập phân',b'{"amount":10.5}'),('JSON hỏng',b'{broken')]
rows=[]
for name,port in [('ASP.NET Core',5095),('FastAPI',5096)]:
    for case,body in cases:
        request=urllib.request.Request(f'http://127.0.0.1:{port}/comparison/validate',data=body,headers={'Content-Type':'application/json'})
        try: response=urllib.request.urlopen(request,timeout=10)
        except urllib.error.HTTPError as error: response=error
        content=json.loads(response.read())
        rows.append({'framework':name,'case':case,'input':body.decode(),'status':response.code,'contentType':response.headers.get('Content-Type'),'trace':response.headers.get('X-Lab-Trace'),'response':content})
        assert response.headers.get('X-Lab-Trace')==('aspnetcore' if port==5095 else 'fastapi')
        if case=='Hợp lệ': assert response.code==200 and content['amount']==10
        if case in ['Bằng không','Số âm','Thiếu trường','Chuỗi chữ','Số thập phân','JSON hỏng']:assert response.code in [400,422]
result={'generatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'validation and middleware only; no database, provider, load or production benchmark','requests':len(rows),'observations':rows}
path=pathlib.Path(__file__).resolve().parents[1]/'docs/report-revision/framework-comparison-results.json'
path.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8')
print('Recorded',len(rows),'real HTTP responses; common safety assertions passed.')
