"""Exercises the migrated API on local PostgreSQL using new QA accounts only."""
import concurrent.futures
import os
import json
import pathlib
import subprocess
import re
import time
import urllib.error
import uuid
import urllib.request

BASE = os.environ.get('WALLET_TEST_BASE', 'http://localhost:5081/api')
results = []
stamp = str(int(time.time()))
password = 'QA-Wallet@123'
pin = '123456'

FINANCIAL = re.compile(r'^/(transactions|transfer|savings|investments|loans|insurance|payments|bank/topup)(/|$)')

def call(method, path, body=None, token=None, status=200, extra_headers=None, auto_key=True):
    headers = {'Content-Type': 'application/json'}
    # Same contract as wallet-web/src/services/transport.ts: every financial mutation carries a key.
    if auto_key and method != 'GET' and FINANCIAL.match(path):
        headers['Idempotency-Key'] = 'qa-auto-' + uuid.uuid4().hex
    headers.update(extra_headers or {})
    if token:
        headers['Authorization'] = 'Bearer ' + token
    request = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            code = response.status
            data = json.load(response)
    except urllib.error.HTTPError as error:
        code = error.code
        raw = error.read().decode('utf-8', errors='replace')
        try:
            data = json.loads(raw)
        except json.JSONDecodeError:
            data = {'message': raw or f'HTTP {code}'}
    assert code == status, f'{method} {path}: expected {status}, received {code}: {data.get("message", "")}'
    results.append({'method': method, 'path': path, 'status': code, 'pass': True})
    return data

def register(n):
    email = f'qa-wallet-{stamp}-{n}@example.test'
    phone = '09' + str(int(stamp[-7:]) * 10 + n).zfill(8)
    user = call('POST', '/auth/register', {'name': f'QA Wallet {n}', 'email': email, 'phone': phone, 'password': password}, status=201)
    login = call('POST', '/auth/login', {'identifier': email, 'password': password})
    return user['userId'], login['token'], email, phone

call('GET', '/health')
first, token, email, phone = register(1)
second, other_token, other_email, other_phone = register(2)
call('POST', '/auth/register', {'name': 'Duplicate', 'email': email, 'phone': phone, 'password': password}, status=409)
call('POST', '/auth/login', {'identifier': email, 'password': 'wrong'}, status=401)
call('GET', f'/wallet/{first}', status=401)
call('GET', f'/wallet/{second}', token=token, status=403)
call('GET', '/admin/stats', token=token, status=403)
call('POST', '/auth/set-pin', {'userId': first, 'pinCode': pin}, token)
call('POST', '/auth/set-pin', {'userId': second, 'pinCode': pin}, other_token)
call('POST', '/loans/create', {'user_id': first, 'pin': pin, 'type': 'Vay mô phỏng', 'amount': 20000000, 'term': 12}, token, status=400)
call('POST', '/loans/create', {'user_id': first, 'pin': pin, 'type': 'Vay tiêu dùng', 'amount': 600000000, 'term': 12}, token, status=400)
products = call('GET', '/loans/products', token=token)['products']
consumer = next(p for p in products if p['type'] == 'Vay tiêu dùng')
assert call('GET', '/savings/products', token=token)['tiers'][0] == {'fromTerm': 0, 'toTerm': 0, 'annualRate': 4.2}
created_loan = call('POST', '/loans/create', {'user_id': first, 'pin': pin, 'type': 'Vay tiêu dùng', 'amount': 20000000, 'interestRate': 0, 'term': 12}, token, status=201)['loan']
assert created_loan['interestRate'] == consumer['annualRate'] == 18, 'Loan rate must come from the server product table, not the client'
loan = created_loan['id']
call('POST', '/loans/create', {'user_id': first, 'pin': pin, 'type': 'Vay mua xe', 'amount': 90000000, 'term': 24}, token, status=403)
before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
call('POST', '/transactions/transfer', {'from_user_id': first, 'to_phone': other_phone, 'amount': -10000, 'pin': pin}, token, status=400)
call('POST', '/transactions/transfer', {'from_user_id': first, 'to_phone': other_phone, 'amount': 10000, 'pin': '000000'}, token, status=401)
call('POST', '/transactions/transfer', {'from_user_id': first, 'to_phone': phone, 'amount': 10000, 'pin': pin}, token, status=400)
invalid = call('POST', '/transactions/transfer', {'from_user_id': first, 'to_phone': other_phone, 'amount': 'abc', 'pin': pin}, token, status=400)
assert invalid['code'] == 'VALIDATION_ERROR' and 'amount' in invalid['errors'], 'Typed binding must report the failing field'
call('POST', '/transactions/transfer', {'from_user_id': first, 'to_phone': other_phone, 'amount': 10000, 'pin': pin}, token, status=400, auto_key=False)
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before
transfer = {'from_user_id': first, 'to_phone': other_phone, 'amount': 10000, 'message': 'QA transfer', 'pin': pin}
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
    list(executor.map(lambda _: call('POST', '/transactions/transfer', transfer, token), range(2)))
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before - 20000
assert call('GET', f'/wallet/{second}', token=other_token)['wallet']['balance'] == 20000
call('GET', f'/transfer/daily-summary/{first}', token=token)
call('POST', '/transfer/execute', {'senderId': first, 'receiverPhone': other_phone, 'amount': 10000, 'pinCode': pin}, token)
created_savings = call('POST', '/savings/create', {'user_id': first, 'pin': pin, 'name': 'QA Savings', 'amount': 1000000, 'term': 3, 'interestRate': 100}, token, status=201)['account']
assert created_savings['interestRate'] == 6.0, 'Savings rate must come from the server term table'
savings = created_savings['id']
call('GET', f'/savings/{first}', token=token)
call('POST', '/savings/close', {'user_id': first, 'pin': pin, 'id': savings}, token)
call('POST', '/savings/close', {'user_id': first, 'pin': pin, 'id': savings}, token, status=400)
flexible = call('POST', '/savings/create', {'user_id': first, 'pin': pin, 'name': 'QA Flexible', 'amount': 500000, 'term': 0, 'interestRate': 4.2}, token, status=201)['account']['id']
call('POST', '/savings/close', {'user_id': first, 'pin': pin, 'id': flexible}, token)
investment = call('POST', '/investments/create', {'user_id': first, 'pin': pin, 'type': 'Cổ phiếu mô phỏng', 'name': 'QA Investment', 'amount': 5000000}, token, status=201)['investment']['id']
call('PUT', '/investments/update', {'user_id': first, 'id': investment, 'currentValue': 900000000}, token, status=403)
before_sell = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
call('POST', '/investments/sell', {'user_id': first, 'pin': pin, 'id': investment, 'sellPrice': 1000000000000}, token)
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before_sell + 5000000, 'Sell must credit the server-held value, never the client sellPrice'
call('POST', '/investments/sell', {'user_id': first, 'pin': pin, 'id': investment}, token, status=400)
call('GET', f'/investments/{first}', token=token)
call('POST', '/loans/payment', {'user_id': first, 'pin': pin, 'id': loan, 'amount': 2000000}, token)
call('GET', f'/loans/{first}', token=token)
before_policy = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
insurance = {'user_id': first, 'type': 'Mô phỏng', 'name': 'QA no PIN', 'premium': 500000, 'coverage': 10000000, 'term': 1}
assert call('POST', '/insurance/create', insurance, token, status=400)['code'] == 'PIN_REQUIRED'
call('POST', '/insurance/create', {**insurance, 'pin': '000000'}, token, status=401)
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before_policy, 'Insurance without a valid PIN must not charge the wallet'
policy = call('POST', '/insurance/create', {'user_id': first, 'pin': pin, 'type': 'Mô phỏng', 'name': 'QA policy', 'premium': 500000, 'coverage': 10000000, 'term': 1}, token, status=201)['policy']['id']
call('POST', '/insurance/renew', {'user_id': first, 'pin': pin, 'id': policy, 'term': 1}, token)
# A refused money operation is kept in history as "failed" with its reason, and moves no money.
poor_before = call('GET', f'/wallet/{second}', token=other_token)['wallet']['balance']
refused = call('POST', '/insurance/create', {'user_id': second, 'pin': pin, 'type': 'Mô phỏng', 'name': 'QA too expensive', 'premium': 9000000, 'coverage': 10000000, 'term': 1}, other_token, status=400)
failed_rows = [t for t in call('GET', f'/transactions/{second}', token=other_token)['transactions'] if t['status'] == 'failed']
assert len(failed_rows) == 1 and failed_rows[0]['amount'] == -9000000 and failed_rows[0]['failure_reason'] == refused['message'], 'Refused purchase must appear once as failed with its reason'
assert call('GET', f'/wallet/{second}', token=other_token)['wallet']['balance'] == poor_before
call('POST', '/insurance/cancel', {'user_id': first, 'id': policy}, token)
call('GET', f'/insurance/{first}', token=token)
card = call('POST', '/cards/add', {'user_id': first, 'card_number': '4111111111111111', 'holder_name': 'QA TEST', 'expiry_month': 12, 'expiry_year': 2030, 'brand': 'VISA', 'type': 'debit', 'issuer': 'QA'}, token, status=201)['card']['card_id']
cards = call('GET', f'/cards/{first}', token=token)['cards']
assert all('card_number_encrypted' not in c for c in cards)
call('PATCH', f'/cards/{card}/status', {'status': 'blocked'}, token)
call('DELETE', f'/cards/{card}', token=token)
banks = call('GET', '/banks/supported')['data']
assert banks
call('POST', '/transactions/phone-topup', {'user_id': first, 'phone_number': phone, 'provider': 'VIETTEL', 'amount': 10000, 'pin': pin}, token)
call('POST', '/transactions/bill-payment', {'user_id': first, 'bill_type': 'ELECTRIC', 'bill_code': 'QA123', 'amount': 10000, 'pin': pin}, token)
kyc = call('POST', '/kyc/submit', {'user_id': first, 'full_name': 'QA Wallet', 'national_id': '999' + stamp[-9:], 'phone': phone, 'email': email, 'date_of_birth': '2000-01-01'}, token)['data']['kyc_id']
call('GET', f'/kyc/status/{first}', token=token)
call('GET', f'/kyc/status/{first}', token=other_token, status=403)
call('GET', f'/transactions/{first}', token=token)
call('GET', f'/notifications/{second}', token=other_token)
call('PATCH', f'/notifications/{second}/read-all', token=other_token)
call('GET', f'/contacts/{first}', token=token)
call('GET', '/merchants', token=token)
call('GET', '/merchants/billers', token=token)
call('GET', f'/security/{first}', token=token)
call('POST', '/security/verify-password', {'user_id': first, 'password': password}, token)
call('POST', '/security/change-pin', {'user_id': first, 'old_pin': pin, 'new_pin': '654321'}, token)
call('POST', '/security/change-password', {'user_id': first, 'old_password': password, 'new_password': 'QA-New@123'}, token)
reset = call('POST', '/auth/forgot-password', {'email': email})['resetToken']
call('POST', '/auth/reset-password', {'token': reset, 'newPassword': password})
call('POST', '/auth/reset-password', {'token': reset, 'newPassword': password}, status=400)
call('POST', '/auth/login', {'identifier': email, 'password': password})
# Privileged test setup is restricted by the helper to localhost and a QA email.
subprocess.run(['dotnet', 'run', '--project', 'scripts/WalletDatabaseTool', '--', '--local-test-admin', email], check=True, stdout=subprocess.DEVNULL)
admin_token = call('POST', '/admin/login', {'email': email, 'password': password})['token']
stats = call('GET', '/admin/stats', token=admin_token)['stats']
assert stats['totalBankAccounts'] == 50
call('GET', '/admin/users', token=admin_token)
call('GET', '/admin/transactions', token=admin_token)
call('GET', '/admin/kyc/requests', token=admin_token)
call('POST', f'/admin/kyc/approve/{kyc}', token=admin_token)
assert call('GET', f'/kyc/status/{first}', token=token)['kyc']['status'] == 'verified'
call('POST', f'/admin/kyc/approve/{kyc}', token=admin_token, status=409)
accounts = call('GET', '/bank-accounts', token=admin_token)['accounts']
account = next(a for a in accounts if a['status'] == 'active' and any(a['bank_name'] == b['bank_name'] and a['bank_code'] == b['bank_code'] for b in banks))
call('POST', '/bank-accounts/verify', {'bank_code': account['bank_code'], 'account_number': account['account_number']}, other_token)
linked = call('POST', '/bank/link', {'user_id': second, 'bank_name': account['bank_name'], 'account_number': account['account_number'], 'account_holder': account['account_name']}, other_token)['data']['bank_id']
call('GET', f'/bank/linked/{second}', token=other_token)
call('PUT', '/bank/set-default', {'user_id': second, 'bank_id': linked}, other_token)
call('POST', '/bank/topup', {'user_id': second, 'linked_bank_id': linked, 'amount': 50000, 'pin_code': pin}, other_token)
call('DELETE', f'/bank/unlink/{linked}', {'user_id': second}, other_token)
call('PATCH', f'/admin/users/{second}/status', {'status': 'suspended'}, admin_token)
call('GET', f'/wallet/{second}', token=other_token, status=403)
call('PATCH', f'/admin/users/{second}/status', {'status': 'active'}, admin_token)
call('GET', '/webhook/health')
call('POST', '/webhook/payment', {'amount': 50000}, status=503)
path = pathlib.Path('docs/wallet-test-results'); path.mkdir(parents=True, exist_ok=True)
(path / 'local-smoke.json').write_text(json.dumps({'passed': len(results), 'checks': results}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'PASS: {len(results)} HTTP checks; concurrent transfers conserve balances; reset token is single-use; admin/ownership boundaries verified.')
