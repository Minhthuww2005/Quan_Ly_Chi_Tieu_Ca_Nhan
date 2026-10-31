"""Time the two requests the Group page makes, with the API's Redis cache unreachable (local QA only).

The page loads GET /api/groups and GET /api/groups/{id}?month=. The script creates a QA user and a group with three
entries, then opens the group detail after different idle times (the old fallback skipped Redis only for 5 s).

  WALLET_TEST_BASE=http://localhost:5085/api python3 scripts/wallet-group-timing.py --label 924145b --out before.json
"""
import argparse, json, os, pathlib, time, urllib.error, urllib.request, uuid

parser = argparse.ArgumentParser()
parser.add_argument('--label', required=True)
parser.add_argument('--out', required=True)
args = parser.parse_args()
BASE = os.environ.get('WALLET_TEST_BASE', 'http://localhost:5082/api')
assert BASE.startswith(('http://localhost:', 'http://127.0.0.1:')), 'Local QA only'


def call(method, path, body=None, token=None):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if method != 'GET':
        headers['Idempotency-Key'] = 'qa-timing-' + uuid.uuid4().hex
    request = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            data, status, cache = json.load(response), response.status, response.headers.get('X-Group-Cache')
    except urllib.error.HTTPError as error:
        data, status, cache = json.loads(error.read() or b'{}'), error.code, error.headers.get('X-Group-Cache')
    return status, data, round((time.perf_counter() - started) * 1000, 1), cache


stamp = str(int(time.time() * 1000))
email = f'qa-wallet-timing-{stamp}@example.test'
assert call('POST', '/auth/register', {'name': 'QA Timing', 'email': email, 'phone': '06' + stamp[-8:], 'password': 'QA-Wallet@123'})[0] == 201
token = call('POST', '/auth/login', {'identifier': email, 'password': 'QA-Wallet@123'})[1]['token']
group = call('POST', '/groups', {'name': 'QA timing', 'budget': 100000}, token)[1]['id']
for i in range(3):
    call('POST', f'/groups/{group}/entries', {'title': f'QA {i}', 'amount': 10000, 'kind': 'expense', 'category': 'Ăn uống', 'date': time.strftime('%Y-%m-%d')}, token)
month = time.strftime('%Y-%m')
samples = []
for idle in (0, 6, 6, 12, 1, 1):
    time.sleep(idle)
    list_status, _, list_ms, _ = call('GET', '/groups', token=token)
    status, detail, ms, cache = call('GET', f'/groups/{group}?month={month}', token=token)
    assert list_status == 200 and status == 200 and len(detail['entries']) == 3
    samples.append({'idleSecondsBefore': idle, 'listMs': list_ms, 'detailMs': ms, 'cache': cache})
result = {'label': args.label, 'api': BASE, 'redis': 'not running', 'generatedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'samples': samples}
pathlib.Path(args.out).write_text(json.dumps(result, indent=2), encoding='utf-8')
print(f"[{args.label}] group detail ms: " + ', '.join(str(s['detailMs']) for s in samples))
