"""Read-only local health workload; descriptive measurements, not production capacity."""
import concurrent.futures,csv,datetime,json,math,pathlib,statistics,time,urllib.request
BASE='http://localhost:5082/api/health'
OUT=pathlib.Path('docs/report-revision');OUT.mkdir(exist_ok=True)
def one(_):
    start=time.perf_counter()
    with urllib.request.urlopen(BASE,timeout=15) as response:
        body=json.load(response)
        if response.status!=200 or body['database']!='PostgreSQL' or not response.headers.get('X-Request-Id'):
            raise RuntimeError('Unexpected health response')
    return (time.perf_counter()-start)*1000
for n in range(20):one(n)
groups={};rows=[]
for workers in [1,20]:
    start=time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:samples=list(pool.map(one,range(200)))
    elapsed=time.perf_counter()-start;ordered=sorted(samples)
    groups[str(workers)]={'samples':200,'elapsedSeconds':elapsed,'completedRequestsPerSecond':200/elapsed,'meanMs':statistics.mean(samples),'medianMs':statistics.median(samples),'p95Ms':ordered[math.ceil(.95*len(samples))-1],'sampleStdDevMs':statistics.stdev(samples),'minimumMs':min(samples),'maximumMs':max(samples)}
    rows.extend({'concurrency':workers,'sample':n+1,'elapsed_ms':value} for n,value in enumerate(samples))
result={'generatedAt':datetime.datetime.now(datetime.timezone.utc).isoformat(),'endpoint':BASE,'warmupRequests':20,'method':'client end-to-end with urllib HTTP connection per request; p95 nearest rank; no TLS; no writes; health database query only','groups':groups}
(OUT/'health-benchmark.json').write_text(json.dumps(result,indent=2),encoding='utf8')
with (OUT/'health-benchmark.csv').open('w',newline='',encoding='utf8') as f:
    w=csv.DictWriter(f,fieldnames=['concurrency','sample','elapsed_ms']);w.writeheader();w.writerows(rows)
print(json.dumps(result,indent=2))
