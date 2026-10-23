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
import collections
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--host', action='append', required=True, help='label=http://host:port/api')
parser.add_argument('--sizes', default='0,10000,100000')
parser.add_argument('--samples', type=int, default=20)
parser.add_argument('--out', default='docs/wallet-test-results')
parser.add_argument('--endpoints', default='history,daily-summary,transfer',
                    help='comma list: history, daily-summary, transfer, admin-stats, admin-users, admin-transactions')
parser.add_argument('--timeout', type=float, default=60, help='seconds; a request slower than this is recorded as a timeout')
parser.add_argument('--users-ratio', type=float, default=0.0, help='also seed this many synthetic users per transaction row')
args = parser.parse_args()
hosts = dict(h.split('=', 1) for h in args.host)
for url in hosts.values():
    if not url.startswith(('http://localhost:', 'http://127.0.0.1:')):
        raise SystemExit('Benchmark writes synthetic data; only local QA hosts are allowed.')
sizes = [int(s) for s in args.sizes.split(',')]


def sql(statement):
    return subprocess.run(['psql', '-v', 'ON_ERROR_STOP=1', '-tAc', statement], check=True, capture_output=True, text=True).stdout.strip()


recent = collections.deque()


def throttle():
    # Stay under the API limiter (120 requests/minute/IP) without disabling it.
    now = time.monotonic()
    while recent and now - recent[0] > 60:
        recent.popleft()
    if len(recent) >= 110:
        time.sleep(60.5 - (now - recent[0]))
    recent.append(time.monotonic())


def call(base, method, path, body=None, token=None, key=False):
    throttle()
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if key:
        headers['Idempotency-Key'] = 'bench-' + uuid.uuid4().hex
    request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=args.timeout) as response:
            data = json.load(response)
            status = response.status
    except urllib.error.HTTPError as error:
        status, data = error.code, json.loads(error.read() or b'{}')
    except TimeoutError:
        status, data = 'timeout', {}
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
endpoints = args.endpoints.split(',')
admin_token = None
if any(e.startswith('admin-') for e in endpoints):
    sql(f"UPDATE htdpay.users SET data = jsonb_set(data, '{{role}}', '\"admin\"'::jsonb) WHERE data->>'user_id' = '{receiver['id']}'")
    admin_email = f'qa-bench-{stamp}-2@example.test'
    status, admin_login, _ = call(first_base, 'POST', '/admin/login', {'email': admin_email, 'password': 'QA-Bench@123'})
    assert status == 200, admin_login
    admin_token = admin_login['token']
sql(f"UPDATE htdpay.wallets SET data = jsonb_set(data, '{{balance}}', '1000000000'::jsonb) WHERE data->>'user_id' = '{sender['id']}'")
# The benchmark is about table size, not daily limits: give the sender the higher KYC tier.
sql(f"UPDATE htdpay.users SET data = jsonb_set(data, '{{kycTier}}', '\"TIER_2\"'::jsonb) WHERE data->>'user_id' = '{sender['id']}'")

sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
sql("DELETE FROM htdpay.users WHERE record_key LIKE 'bench-u-%'")
seeded = 0
seeded_users = 0
rows, summary = [], []
timed_out = set()
for size in sizes:
    if size > seeded:
        sql(f"""INSERT INTO htdpay.transactions(record_key, data)
                SELECT 'bench-' || g, jsonb_build_object('trans_id', -g, 'wallet_id', 900000 + g % 5000, 'amount', -10000,
                       'type', 'transfer', 'status', 'success', 'timestamp', '2026-01-01T00:00:00.0000000+00:00', 'title', 'bench',
                       'description', 'synthetic benchmark row', 'reference', md5(g::text))
                FROM generate_series({seeded + 1}, {size}) g""")
        sql('ANALYZE htdpay.transactions')
        seeded = size
    target_users = int(size * args.users_ratio)
    if target_users > seeded_users:
        sql(f"""INSERT INTO htdpay.users(record_key, data)
                SELECT 'bench-u-' || g, jsonb_build_object('user_id', 9000000 + g, 'name', 'Bench user ' || g, 'email', 'bench-' || g || '@example.test',
                       'phone', '06' || lpad(g::text, 8, '0'), 'role', 'user', 'status', 'active', 'accountStatus', 'active', 'kycTier', 'TIER_1',
                       'created_at', '2026-01-01T00:00:00.0000000+00:00')
                FROM generate_series({seeded_users + 1}, {target_users}) g""")
        sql('ANALYZE htdpay.users')
        seeded_users = target_users
    table_rows = int(sql('SELECT count(*) FROM htdpay.transactions'))
    for label, base in hosts.items():
        for endpoint in endpoints:
            if (label, endpoint) in timed_out:
                # Already slower than --timeout at a smaller table: larger tables are not re-tried.
                summary.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'timeout': True, 'note': 'timed out at a smaller size'})
                print(f"{label:>4} rows={table_rows:>7} {endpoint:<13} skipped (timed out at a smaller size)")
                continue
            for i in range(args.samples):
                if endpoint == 'history':
                    status, data, ms = call(base, 'GET', f"/transactions/{sender['id']}?limit=50", token=sender['token'])
                elif endpoint == 'daily-summary':
                    status, data, ms = call(base, 'GET', f"/transfer/daily-summary/{sender['id']}", token=sender['token'])
                elif endpoint == 'admin-stats':
                    status, data, ms = call(base, 'GET', '/admin/stats', token=admin_token)
                elif endpoint == 'admin-users':
                    status, data, ms = call(base, 'GET', '/admin/users?limit=20', token=admin_token)
                elif endpoint == 'admin-transactions':
                    status, data, ms = call(base, 'GET', '/admin/transactions?limit=20', token=admin_token)
                else:
                    status, data, ms = call(base, 'POST', '/transfer/execute', {'senderId': sender['id'], 'receiverPhone': receiver['phone'], 'amount': 10000, 'pinCode': '123456'}, sender['token'], key=True)
                if status == 'timeout':
                    timed_out.add((label, endpoint))
                    rows.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'sample': i + 1, 'ms': f'timeout>{args.timeout:g}s'})
                    break
                assert status == 200, f'{label} {endpoint} at {table_rows} rows -> {status}: {data}'
                rows.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'sample': i + 1, 'ms': round(ms, 2)})
            if (label, endpoint) in timed_out:
                summary.append({'host': label, 'table_rows': table_rows, 'endpoint': endpoint, 'timeout': True, 'timeout_s': args.timeout})
                print(f"{label:>4} rows={table_rows:>7} {endpoint:<13} TIMEOUT (> {args.timeout:g} s)")
                continue
            samples = sorted(r['ms'] for r in rows if r['host'] == label and r['table_rows'] == table_rows and r['endpoint'] == endpoint and isinstance(r['ms'], float))
            summary.append({'host': label, 'table_rows': table_rows, 'user_rows': int(sql('SELECT count(*) FROM htdpay.users')), 'endpoint': endpoint, 'samples': len(samples),
                            'median_ms': round(statistics.median(samples), 1), 'p95_ms': round(samples[max(0, int(round(0.95 * len(samples))) - 1)], 1),
                            'max_ms': round(samples[-1], 1)})
            print(f"{label:>4} rows={table_rows:>7} {endpoint:<13} median={summary[-1]['median_ms']:>8} ms p95={summary[-1]['p95_ms']:>8} ms")

sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
sql("DELETE FROM htdpay.users WHERE record_key LIKE 'bench-u-%'")
out = pathlib.Path(args.out); out.mkdir(parents=True, exist_ok=True)
with open(out / 'scale-benchmark-samples.csv', 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=['host', 'table_rows', 'endpoint', 'sample', 'ms']); writer.writeheader(); writer.writerows(rows)
(out / 'scale-benchmark.json').write_text(json.dumps({
    'generatedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'hosts': hosts, 'samplesPerCell': args.samples,
    'method': 'client wall time per sequential request (urllib, no keep-alive), local PostgreSQL; synthetic rows belong to other wallets',
    'machine': platform.platform(), 'summary': summary}, indent=2), encoding='utf-8')
print(f'Wrote {out / "scale-benchmark.json"} and raw samples CSV ({len(rows)} samples).')
