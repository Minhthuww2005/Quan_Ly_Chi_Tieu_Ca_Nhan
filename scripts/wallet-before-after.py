"""One money-flow invariant measured on two builds of the API (local QA only).

Scenario: a new QA user buys an investment for 5,000,000 VND and sells it while sending sellPrice = 50,000,000.
Invariant: selling may only return the value the server holds (here the purchase amount), so the user's
balance after buy + sell equals the balance before, and the total of all wallet balances does not grow.

Run once against each API and keep both JSON files:
  WALLET_TEST_BASE=http://localhost:5084/api python3 scripts/wallet-before-after.py --label bf21d48 --out <dir>/before.json
  WALLET_TEST_BASE=http://localhost:5082/api python3 scripts/wallet-before-after.py --label f8cd0f8 --out <dir>/after.json
PGHOST/PGPORT/PGUSER/PGPASSWORD/PGDATABASE must point to the same local database as the API.
"""
import argparse
import json
import os
import pathlib
import subprocess
import time
import urllib.error
import urllib.request
import uuid

parser = argparse.ArgumentParser()
parser.add_argument('--label', required=True)
parser.add_argument('--out', required=True)
args = parser.parse_args()
BASE = os.environ.get('WALLET_TEST_BASE', 'http://localhost:5082/api')
assert BASE.startswith(('http://localhost:', 'http://127.0.0.1:')), 'Local QA only'
assert os.environ.get('PGHOST', 'localhost') in ('localhost', '127.0.0.1'), 'Local QA database only'
PASSWORD, PIN, BUY, SELL_PRICE = 'QA-Wallet@123', '123456', 5_000_000, 50_000_000


def call(method, path, body=None, token=None):
    headers = {'Content-Type': 'application/json', 'Idempotency-Key': 'qa-ba-' + uuid.uuid4().hex}
    if token:
        headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            return response.status, json.load(response)
    except urllib.error.HTTPError as error:
        return error.code, json.loads(error.read() or b'{}')


def sql(query):
    return subprocess.run(['psql', '-tAc', query], check=True, capture_output=True, text=True).stdout.strip()


def total_balance():
    return float(sql("SELECT COALESCE(sum((data->>'balance')::numeric), 0) FROM htdpay.wallets"))


stamp = str(int(time.time() * 1000))
email = f'qa-wallet-ba-{stamp}@example.test'
status, user = call('POST', '/auth/register', {'name': 'QA Before After', 'email': email, 'phone': '07' + stamp[-8:], 'password': PASSWORD})
assert status == 201, (status, user)
uid = user['userId']
token = call('POST', '/auth/login', {'identifier': email, 'password': PASSWORD})[1]['token']
assert call('POST', '/auth/set-pin', {'userId': uid, 'pinCode': PIN}, token)[0] == 200
# The fixture gives new accounts no money; credit 20,000,000 directly so both builds start from the same state.
sql(f"UPDATE htdpay.wallets SET data = jsonb_set(data, '{{balance}}', '20000000'::jsonb) WHERE data->>'user_id' = '{uid}'")
balance = lambda: float(call('GET', f'/wallet/{uid}', token=token)[1]['wallet']['balance'])
user_before, total_before = balance(), total_balance()
buy = call('POST', '/investments/create', {'user_id': uid, 'name': 'QA Stock', 'type': 'Cổ phiếu mô phỏng', 'amount': BUY, 'pin': PIN}, token)
assert buy[0] in (200, 201), buy
investment = (buy[1].get('investment') or {}).get('id')
sell = call('POST', '/investments/sell', {'user_id': uid, 'id': investment, 'sellPrice': SELL_PRICE, 'pin': PIN}, token)
assert sell[0] == 200, sell
user_after, total_after = balance(), total_balance()
result = {
    'label': args.label, 'api': BASE, 'generatedAt': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()),
    'scenario': f'buy investment {BUY}, sell with client sellPrice {SELL_PRICE}',
    'buyStatus': buy[0], 'sellStatus': sell[0], 'sellResponse': sell[1],
    'userBalanceBefore': user_before, 'userBalanceAfter': user_after, 'userBalanceChange': user_after - user_before,
    'totalWalletBalanceBefore': total_before, 'totalWalletBalanceAfter': total_after, 'totalChange': total_after - total_before,
    'invariantHolds': user_after == user_before and total_after == total_before,
}
pathlib.Path(args.out).parent.mkdir(parents=True, exist_ok=True)
pathlib.Path(args.out).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
print(('PASS' if result['invariantHolds'] else 'FAIL') + f" [{args.label}] user balance change {result['userBalanceChange']:+,.0f} VND, total wallets change {result['totalChange']:+,.0f} VND")
