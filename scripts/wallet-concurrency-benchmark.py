"""Concurrent transfer load on the local QA API: throughput, latency and balance conservation.

Local QA only. Each virtual user has its own sender wallet and its own loopback source address
(127.0.0.N), the way separate clients would reach the API, so the per-IP rate limiter (120/min)
is not what is being measured. Every level sends `level` transfers at the same moment, `rounds` times.
After all levels the script checks that money was conserved: the total of all benchmark wallets is
unchanged and each sender lost exactly (successful transfers x amount).

Usage:
  PGHOST=localhost PGPORT=55432 PGUSER=revenueflow PGPASSWORD=... PGDATABASE=revenueflow_demo \
  python3 scripts/wallet-concurrency-benchmark.py --base http://localhost:5082/api --levels 1,10,25,50 --rounds 5
"""
import argparse
import csv
import http.client
import json
import pathlib
import platform
import statistics
import subprocess
import threading
import time
import urllib.parse
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--base', default='http://localhost:5082/api')
parser.add_argument('--levels', default='1,10,25,50')
parser.add_argument('--rounds', type=int, default=5)
parser.add_argument('--amount', type=int, default=10000)
parser.add_argument('--out', default='docs/wallet-test-results')
args = parser.parse_args()
url = urllib.parse.urlparse(args.base)
if url.hostname not in ('localhost', '127.0.0.1'):
    raise SystemExit('Benchmark writes QA data; only a local QA host is allowed.')
levels = [int(x) for x in args.levels.split(',')]
users_needed = max(levels)


def sql(statement):
    return subprocess.run(['psql', '-v', 'ON_ERROR_STOP=1', '-tAc', statement], check=True, capture_output=True, text=True).stdout.strip()


def call(source_ip, method, path, body=None, token=None, key=False):
    connection = http.client.HTTPConnection(url.hostname, url.port, timeout=120, source_address=(source_ip, 0))
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    if key:
        headers['Idempotency-Key'] = 'load-' + uuid.uuid4().hex
    started = time.perf_counter()
    connection.request(method, url.path + path, body=json.dumps(body) if body is not None else None, headers=headers)
    response = connection.getresponse()
    data = json.loads(response.read() or b'{}')
    elapsed = (time.perf_counter() - started) * 1000
    connection.close()
    return response.status, data, elapsed


stamp = str(int(time.time()))
users = []
for n in range(users_needed * 2):  # a sender and a receiver per virtual user
    ip = f'127.0.{1 + n // 250}.{2 + n % 250}'
    email = f'qa-load-{stamp}-{n}@example.test'
    phone = '07' + str(int(stamp[-6:]) * 1000 + n).zfill(8)
    status, user, _ = call(ip, 'POST', '/auth/register', {'name': f'QA Load {n}', 'email': email, 'phone': phone, 'password': 'QA-Load@123'})
    assert status == 201, user
    _, login, _ = call(ip, 'POST', '/auth/login', {'identifier': email, 'password': 'QA-Load@123'})
    status, _, _ = call(ip, 'POST', '/auth/set-pin', {'userId': user['userId'], 'pinCode': '123456'}, login['token'])
    assert status == 200
    users.append({'id': user['userId'], 'token': login['token'], 'phone': phone, 'ip': ip})
senders, receivers = users[:users_needed], users[users_needed:]
ids = ','.join(f"'{u['id']}'" for u in users)
sql(f"UPDATE htdpay.wallets SET data = jsonb_set(data, '{{balance}}', '100000000'::jsonb) WHERE data->>'user_id' IN ({','.join(repr(str(u['id'])) for u in senders)})")
sql(f"UPDATE htdpay.users SET data = jsonb_set(data, '{{kycTier}}', '\"TIER_2\"'::jsonb) WHERE data->>'user_id' IN ({ids})")


def balances():
    rows = sql(f"SELECT data->>'user_id', data->>'balance' FROM htdpay.wallets WHERE data->>'user_id' IN ({ids})")
    return {int(r.split('|')[0]): float(r.split('|')[1]) for r in rows.splitlines()}


before = balances()
samples, summary = [], []
sent = {u['id']: 0 for u in senders}
time.sleep(61)  # setup requests used the per-IP budget of this minute
for level in levels:
    started = time.perf_counter()
    for round_no in range(args.rounds):
        barrier = threading.Barrier(level)
        results = [None] * level

        def worker(i):
            s, r = senders[i], receivers[i]
            barrier.wait()
            status, data, ms = call(s['ip'], 'POST', '/transfer/execute', {'senderId': s['id'], 'receiverPhone': r['phone'], 'amount': args.amount, 'pinCode': '123456'}, s['token'], key=True)
            results[i] = (status, ms, data.get('code', ''))

        threads = [threading.Thread(target=worker, args=(i,)) for i in range(level)]
        for t in threads: t.start()
        for t in threads: t.join()
        for i, (status, ms, code) in enumerate(results):
            samples.append({'level': level, 'round': round_no + 1, 'user': i, 'status': status, 'code': code, 'ms': round(ms, 2)})
            if status == 200:
                sent[senders[i]['id']] += 1
    wall = time.perf_counter() - started
    cell = [x for x in samples if x['level'] == level]
    ok = sorted(x['ms'] for x in cell if x['status'] == 200)
    summary.append({'level': level, 'requests': len(cell), 'ok': len(ok), 'errors': len(cell) - len(ok),
                    'throughput_rps': round(len(ok) / wall, 1), 'median_ms': round(statistics.median(ok), 1) if ok else None,
                    'p95_ms': round(ok[max(0, int(round(0.95 * len(ok))) - 1)], 1) if ok else None, 'max_ms': round(ok[-1], 1) if ok else None})
    print(f"level={level:>3} ok={summary[-1]['ok']:>4}/{len(cell):<4} {summary[-1]['throughput_rps']:>6} req/s median={summary[-1]['median_ms']} ms p95={summary[-1]['p95_ms']} ms")
    time.sleep(61)

after = balances()
conserved = abs(sum(after.values()) - sum(before.values())) < 0.5
exact = all(abs(before[s['id']] - after[s['id']] - sent[s['id']] * args.amount) < 0.5 for s in senders)
print(f'Conservation: total unchanged={conserved}; each sender debited exactly once per success={exact}')
out = pathlib.Path(args.out); out.mkdir(parents=True, exist_ok=True)
with open(out / 'concurrency-samples.csv', 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=['level', 'round', 'user', 'status', 'code', 'ms']); writer.writeheader(); writer.writerows(samples)
(out / 'concurrency-benchmark.json').write_text(json.dumps({
    'generatedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'base': args.base, 'rounds': args.rounds, 'amount': args.amount,
    'method': 'level = simultaneous transfers (thread barrier), one sender wallet and one loopback source IP per virtual user; client wall time; local PostgreSQL',
    'machine': platform.platform(), 'cpu_count': __import__('os').cpu_count(), 'summary': summary,
    'conservation': {'total_unchanged': conserved, 'each_sender_debited_exactly': exact}}, indent=2), encoding='utf-8')
if not (conserved and exact):
    raise SystemExit('Balance conservation failed')
