"""Measures how HTD PAY endpoints scale with the size of htdpay.transactions.

Local QA only. Seeds synthetic rows (record_key 'bench-*', wallets 900000+) with psql, then times
history reads, daily-summary reads and transfers against one or more API hosts that share the database.
Raw samples are written next to the summary so the numbers can be re-checked.

Synthetic rows use negative trans_id so they never collide with ids allocated by MAX+1.

Usage:
  PGHOST=localhost PGPORT=55432 PGUSER=revenueflow PGPASSWORD=... PGDATABASE=revenueflow_demo \
  python3 scripts/wallet-scale-benchmark.py --host new=http://localhost:5082/api --host old=http://localhost:5083/api \
      --sizes 0,10000,100000 --samples 20
"""
import argparse
import csv
import json
import pathlib
import platform
import statistics
import subprocess
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--host', action='append', required=True, help='label=http://host:port/api')
parser.add_argument('--sizes', default='0,10000,100000')
parser.add_argument('--samples', type=int, default=20)
parser.add_argument('--out', default='docs/wallet-test-results')
args = parser.parse_args()
hosts = dict(h.split('=', 1) for h in args.host)
for url in hosts.values():
    if not url.startswith(('http://localhost:', 'http://127.0.0.1:')):
        raise SystemExit('Benchmark writes synthetic data; only local QA hosts are allowed.')
sizes = [int(s) for s in args.sizes.split(',')]


def sql(statement):
    return subprocess.run(['psql', '-v', 'ON_ERROR_STOP=1', '-tAc', statement], check=True, capture_output=True, text=True).stdout.strip()


def call(base, method, path, body=None, token=None, key=False):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if key:
        headers['Idempotency-Key'] = 'bench-' + uuid.uuid4().hex
    request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            data = json.load(response)
            status = response.status
    except urllib.error.HTTPError as error:
        status, data = error.code, json.loads(error.read() or b'{}')
    return status, data, (time.perf_counter() - started) * 1000


first_base = next(iter(hosts.values()))
stamp = str(int(time.time()))
users = []
for n in (1, 2):
    email = f'qa-bench-{stamp}-{n}@example.test'
    phone = '08' + str(int(stamp[-7:]) * 10 + n).zfill(8)
    status, user, _ = call(first_base, 'POST', '/auth/register', {'name': f'QA Bench {n}', 'email': email, 'phone': phone, 'password': 'QA-Bench@123'})
    assert status == 201, user
    status, login, _ = call(first_base, 'POST', '/auth/login', {'identifier': email, 'password': 'QA-Bench@123'})
    call(first_base, 'POST', '/auth/set-pin', {'userId': user['userId'], 'pinCode': '123456'}, login['token'])
    users.append({'id': user['userId'], 'token': login['token'], 'phone': phone})
sender, receiver = users
sql(f"UPDATE htdpay.wallets SET data = jsonb_set(data, '{{balance}}', '1000000000'::jsonb) WHERE data->>'user_id' = '{sender['id']}'")
# The benchmark is about table size, not daily limits: give the sender the higher KYC tier.
sql(f"UPDATE htdpay.users SET data = jsonb_set(data, '{{kycTier}}', '\"TIER_2\"'::jsonb) WHERE data->>'user_id' = '{sender['id']}'")

sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
seeded = 0
rows, summary = [], []
for size in sizes:
    if size > seeded:
        sql(f"""INSERT INTO htdpay.transactions(record_key, data)
                SELECT 'bench-' || g, jsonb_build_object('trans_id', -g, 'wallet_id', 900000 + g % 5000, 'amount', -10000,
                       'type', 'transfer', 'status', 'success', 'timestamp', '2026-01-01T00:00:00.0000000+00:00', 'title', 'bench',
                       'description', 'synthetic benchmark row', 'reference', md5(g::text))
                FROM generate_series({seeded + 1}, {size}) g""")
        sql('ANALYZE htdpay.transactions')
        seeded = size
    table_rows = int(sql('SELECT count(*) FROM htdpay.transactions'))
    for label, base in hosts.items():
        for endpoint in ('history', 'daily-summary', 'transfer'):
            for i in range(args.samples):
                if endpoint == 'history':
                    status, data, ms = call(base, 'GET', f"/transactions/{sender['id']}?limit=50", token=sender['token'])
                elif endpoint == 'daily-summary':
                    status, data, ms = call(base, 'GET', f"/transfer/daily-summary/{sender['id']}", token=sender['token'])
                else:
                    status, data, ms = call(base, 'POST', '/transfer/execute', {'senderId': sender['id'], 'receiverPhone': receiver['phone'], 'amount': 10000, 'pinCode': '123456'}, sender['token'], key=True)
                assert status == 200, f'{label} {endpoint} at {table_rows} rows -> {status}: {data}'
                rows.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'sample': i + 1, 'ms': round(ms, 2)})
            samples = sorted(r['ms'] for r in rows if r['host'] == label and r['table_rows'] == table_rows and r['endpoint'] == endpoint)
            summary.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'samples': len(samples),
                            'median_ms': round(statistics.median(samples), 1), 'p95_ms': round(samples[max(0, int(round(0.95 * len(samples))) - 1)], 1),
                            'max_ms': round(samples[-1], 1)})
            print(f"{label:>4} rows={table_rows:>7} {endpoint:<13} median={summary[-1]['median_ms']:>8} ms p95={summary[-1]['p95_ms']:>8} ms")
        time.sleep(61)  # stay under the 120 requests/minute/IP limiter for the next host

sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
out = pathlib.Path(args.out); out.mkdir(parents=True, exist_ok=True)
with open(out / 'scale-benchmark-samples.csv', 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=['host', 'table_rows', 'endpoint', 'sample', 'ms']); writer.writeheader(); writer.writerows(rows)
(out / 'scale-benchmark.json').write_text(json.dumps({
    'generatedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'hosts': hosts, 'samplesPerCell': args.samples,
    'method': 'client wall time per sequential request (urllib, no keep-alive), local PostgreSQL; synthetic rows belong to other wallets',
    'machine': platform.platform(), 'summary': summary}, indent=2), encoding='utf-8')
print(f'Wrote {out / "scale-benchmark.json"} and raw samples CSV ({len(rows)} samples).')
