"""EXPLAIN (ANALYZE, BUFFERS) of the SQL that HTD PAY runs on its hot paths, on a table of a given size.

Local QA only. Seeds the same synthetic rows as wallet-scale-benchmark.py (record_key 'bench-*'), runs each
query the repository sends (same SQL as PostgresWalletRepository), saves the raw plans, then removes the rows.

Usage:
  PGHOST=localhost PGPORT=55432 PGUSER=revenueflow PGPASSWORD=... PGDATABASE=revenueflow_demo \
  python3 scripts/wallet-explain.py --rows 300000 --users-ratio 0.1 --out docs/htdpay-evidence/<date>-explain
"""
import argparse
import datetime
import json
import os
import pathlib
import platform
import re
import statistics
import subprocess

parser = argparse.ArgumentParser()
parser.add_argument('--rows', type=int, default=300000)
parser.add_argument('--users-ratio', type=float, default=0.1)
parser.add_argument('--repeat', type=int, default=5, help='EXPLAIN ANALYZE runs per query; the median is reported')
parser.add_argument('--out', required=True)
args = parser.parse_args()
if os.environ.get('PGHOST', 'localhost') not in ('localhost', '127.0.0.1'):
    raise SystemExit('Seeds synthetic data; only a local QA database is allowed.')


def sql(statement):
    return subprocess.run(['psql', '-v', 'ON_ERROR_STOP=1', '-tAc', statement], check=True, capture_output=True, text=True).stdout.strip()


# The queries below are the repository's own SQL (PostgresWalletRepository: FindOne, Where, Next, AdminUsers,
# AdminTransactions) with the parameters filled in. Keep them in sync when the repository SQL changes.
NUM = lambda f: (f"(CASE jsonb_typeof(data->'{f}') WHEN 'number' THEN (data->>'{f}')::numeric "
                 f"WHEN 'string' THEN (CASE WHEN data->>'{f}' ~ '^-?[0-9]+(\\.[0-9]+)?([eE][-+]?[0-9]+)?$' THEN (data->>'{f}')::numeric ELSE 0 END) ELSE 0 END)")
AMOUNT, FEE, TODAY = NUM('amount'), NUM('fee'), "'2026-10-24'"
QUERIES = {
    'history-by-wallet': ("Lịch sử / hạn mức ngày: Where(transactions, wallet_id)",
        "SELECT record_key,data::text FROM htdpay.transactions WHERE data->>'wallet_id' = '900001' ORDER BY created_at, ctid"),
    'user-by-email': ("Đăng nhập: FindOne(users, email, ignoreCase)",
        "SELECT record_key,data::text FROM htdpay.users WHERE lower(data->>'email') = lower('bench-12345@example.test') ORDER BY created_at, ctid LIMIT 1"),
    'next-trans-id': ("Next(transactions, trans_id): MAX + 1",
        "SELECT COALESCE(max(CASE WHEN data->>'trans_id' ~ '^[0-9]{1,9}$' THEN (data->>'trans_id')::int END), 0) FROM htdpay.transactions"),
    'admin-transactions-page': ("Admin giao dịch: một trang 20 dòng",
        """SELECT t.data::text, u.data->>'name' AS _sender_name
           FROM (SELECT * FROM htdpay.transactions ORDER BY COALESCE(data->>'timestamp', '') DESC, record_key LIMIT 20 OFFSET 0) t
           LEFT JOIN htdpay.wallets w ON w.data->>'wallet_id' = t.data->>'wallet_id'
           LEFT JOIN htdpay.users u ON u.data->>'user_id' = w.data->>'user_id'
           ORDER BY COALESCE(t.data->>'timestamp', '') DESC, t.record_key"""),
    'admin-transactions-stats': ("Admin giao dịch: thống kê trạng thái và khối lượng",
        f"""SELECT jsonb_build_object()::text,
             count(*) AS total,
             count(*) FILTER (WHERE data->>'status' IN ('success','completed')) AS completed,
             count(*) FILTER (WHERE data->>'status' = 'pending') AS pending,
             count(*) FILTER (WHERE data->>'status' = 'failed') AS failed,
             COALESCE(sum(-{AMOUNT}) FILTER (WHERE data->>'status' IN ('success','completed') AND {AMOUNT} < 0), 0) AS total_volume,
             COALESCE(sum({FEE}) FILTER (WHERE data->>'status' IN ('success','completed')), 0) AS total_fees,
             count(*) FILTER (WHERE data->>'timestamp' LIKE {TODAY} || '%') AS today_transactions,
             COALESCE(sum(-{AMOUNT}) FILTER (WHERE data->>'status' IN ('success','completed') AND data->>'timestamp' LIKE {TODAY} || '%' AND {AMOUNT} < 0), 0) AS today_volume
           FROM htdpay.transactions"""),
    'admin-users-page': ("Admin người dùng: một trang 20 dòng",
        """SELECT u.data::text, w.data->>'balance' AS _balance,
             CASE WHEN w.record_key IS NULL THEN 0 ELSE (SELECT count(*) FROM htdpay.transactions t WHERE t.data->>'wallet_id' = w.data->>'wallet_id') END AS _transaction_count,
             (SELECT k.data->>'status' FROM htdpay.kycprofiles k WHERE k.owner_id = u.owner_id ORDER BY k.created_at DESC, k.ctid DESC LIMIT 1) AS _kyc_status
           FROM htdpay.users u
           LEFT JOIN htdpay.wallets w ON w.data->>'user_id' = u.data->>'user_id'
           ORDER BY COALESCE(u.data->>'created_at', '') DESC, u.record_key
           LIMIT 20 OFFSET 0"""),
}

out = pathlib.Path(args.out)
out.mkdir(parents=True, exist_ok=True)
sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
sql("DELETE FROM htdpay.users WHERE record_key LIKE 'bench-u-%'")
try:
    sql(f"""INSERT INTO htdpay.transactions(record_key, data)
            SELECT 'bench-' || g, jsonb_build_object('trans_id', -g, 'wallet_id', 900000 + g % 5000, 'amount', -10000,
                   'type', 'transfer', 'status', 'success', 'timestamp', '2026-01-01T00:00:00.0000000+00:00', 'title', 'bench',
                   'description', 'synthetic benchmark row', 'reference', md5(g::text))
            FROM generate_series(1, {args.rows}) g""")
    users = int(args.rows * args.users_ratio)
    sql(f"""INSERT INTO htdpay.users(record_key, data)
            SELECT 'bench-u-' || g, jsonb_build_object('user_id', 9000000 + g, 'name', 'Bench user ' || g, 'email', 'bench-' || g || '@example.test',
                   'phone', '06' || lpad(g::text, 8, '0'), 'role', 'user', 'status', 'active', 'accountStatus', 'active', 'kycTier', 'TIER_1',
                   'created_at', '2026-01-01T00:00:00.0000000+00:00')
            FROM generate_series(1, {users}) g""")
    sql('ANALYZE htdpay.transactions; ANALYZE htdpay.users; ANALYZE htdpay.wallets')
    rows = int(sql('SELECT count(*) FROM htdpay.transactions'))
    summary = []
    for name, (label, query) in QUERIES.items():
        plans = [sql('EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) ' + query) for _ in range(args.repeat)]
        times = [float(re.search(r'Execution Time: ([\d.]+) ms', p).group(1)) for p in plans]
        median = statistics.median(times)
        plan = plans[-1]  # the last run, after the first runs have warmed the shared buffers
        (out / f'explain-{name}.txt').write_text(
            f'-- {label}\n-- htdpay.transactions rows: {rows}\n-- execution ms per run: {times}\n{query}\n\n{plan}\n', encoding='utf8')
        scans = sorted(set(re.findall(r'(Index Only Scan|Index Scan|Bitmap Heap Scan|Seq Scan)(?: Backward)?(?: using \w+)? on (\w+)', plan)))
        indexes = sorted(set(re.findall(r'using (\w+)', plan)) | set(re.findall(r'Bitmap Index Scan on (\w+)', plan)))
        summary.append({'query': name, 'label': label, 'executionMs': median, 'runsMs': times,
                        'scans': [f'{kind} on {table}' for kind, table in scans if not table.startswith(('ix_', 'ux_'))], 'indexes': indexes})
        print(f'{name:<26} median {median:>9.3f} ms  runs {times}  {", ".join(f"{k} on {t}" for k, t in scans if not t.startswith(("ix_", "ux_")))}')
    (out / 'explain-summary.json').write_text(json.dumps({
        'generatedAt': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'transactionsRows': rows,
        'syntheticUsers': users, 'repeat': args.repeat, 'host': {'os': platform.platform(), 'cpus': os.cpu_count()}, 'postgres': sql('SHOW server_version'), 'queries': summary}, ensure_ascii=False, indent=2), encoding='utf8')
finally:
    sql("DELETE FROM htdpay.transactions WHERE record_key LIKE 'bench-%'")
    sql("DELETE FROM htdpay.users WHERE record_key LIKE 'bench-u-%'")
