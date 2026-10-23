"""Runs the base suite and additional scenarios only against the local QA server."""
import concurrent.futures
import hashlib
import hmac
import json
import os
import pathlib
import runpy
import time
import urllib.parse

os.environ.setdefault('WALLET_TEST_BASE', 'http://localhost:5082/api')
if os.environ['WALLET_TEST_BASE'] != 'http://localhost:5082/api':
    raise RuntimeError('Extended payment fixtures are restricted to the QA API on port 5082.')
suite = runpy.run_path(str(pathlib.Path(__file__).with_name('wallet-smoke.py')))
call = suite['call']
first, second = suite['first'], suite['second']
token, other_token, admin_token = suite['token'], suite['other_token'], suite['admin_token']
stamp, pin, other_phone = suite['stamp'], '654321', suite['other_phone']  # base suite changes the first user's PIN
results = suite['results']
# Preserve the production rate limit; start extended scenarios in the next window.
time.sleep(60)
config = call('GET', '/auth/config')
assert config['vnpayEnabled'] and config['googleClientId'] == 'qa-google-client.apps.googleusercontent.com'
call('POST', '/auth/google', {'credential': 'malformed-token'}, status=401)
call('GET', '/auth/me', token=token)
call('PATCH', '/auth/profile', {'name': 'QA Updated', 'phone': suite['phone'], 'settings': {'notificationsEnabled': True, 'hideBalance': True}, 'role': 'admin'}, token)
assert call('GET', '/auth/me', token=token)['user']['role'] == 'admin'  # promoted only by the local helper in the base suite
call('PATCH', '/auth/profile', {'phone': suite['phone'], 'role': 'admin'}, other_token, status=409)
call('PATCH', '/auth/profile', {'name': 'QA Other', 'role': 'admin'}, other_token)
assert call('GET', '/auth/me', token=other_token)['user']['role'] == 'user'

before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
transfer = {'senderId': first, 'receiverPhone': other_phone, 'amount': 10000, 'pinCode': pin}
key = {'Idempotency-Key': 'qa-transfer-' + stamp}
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    responses = list(pool.map(lambda _: call('POST', '/transfer/execute', transfer, token, extra_headers=key), range(2)))
assert responses[0]['data']['transactionId'] == responses[1]['data']['transactionId']
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before - 10000
call('POST', '/transfer/execute', {**transfer, 'amount': 20000}, token, status=409, extra_headers=key)
bill = {'user_id': first, 'bill_type': 'electricity', 'bill_code': 'QA-EXT-' + stamp, 'amount': 10000, 'pin': pin}
bill_key = {'Idempotency-Key': 'qa-bill-' + stamp}
paid = call('POST', '/transactions/bill-payment', bill, token, extra_headers=bill_key)
repeat = call('POST', '/transactions/bill-payment', bill, token, extra_headers=bill_key)
assert paid['transaction']['trans_id'] == repeat['transaction']['trans_id']
call('POST', '/transactions/bill-payment', bill, token, status=409)
call('POST', '/payments/vnpay/create', {'type': 'wallet_topup', 'amount': 100000, 'pin': '000000'}, token, status=401)
assert call('GET', '/payments/vnpay/ipn?vnp_SecureHash=bad')['RspCode'] == '97'

SECRET = b'QA_ONLY_VNPAY_SECRET_NOT_A_MERCHANT_KEY'
def callback(order, amount=None, response='00', transaction='00', reference=None, merchant='TESTHTD1', endpoint='ipn'):
    fields = {'vnp_TmnCode': merchant, 'vnp_TxnRef': reference or order['order_id'], 'vnp_Amount': str(int((amount if amount is not None else order['amount']) * 100)), 'vnp_ResponseCode': response, 'vnp_TransactionStatus': transaction, 'vnp_TransactionNo': '123456789'}
    canonical = urllib.parse.urlencode(sorted(fields.items()))
    fields['vnp_SecureHash'] = hmac.new(SECRET, canonical.encode(), hashlib.sha512).hexdigest()
    return call('GET', '/payments/vnpay/' + endpoint + '?' + urllib.parse.urlencode(fields))

order = call('POST', '/payments/vnpay/create', {'type': 'wallet_topup', 'amount': 100000, 'pin': pin}, token, status=201)['order']
before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
assert callback(order, endpoint='verify')['signatureValid']
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before, 'Return URL must never credit the wallet'
assert callback(order, amount=200000)['RspCode'] == '04'
assert callback(order, reference='missing-order')['RspCode'] == '01'
assert callback(order, merchant='OTHERHTD')['RspCode'] == '97'
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    responses = list(pool.map(lambda _: callback(order), range(2)))
assert sorted(r['RspCode'] for r in responses) == ['00', '02']
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before + 100000
assert call('GET', f'/payments/status/{order["order_id"]}', token=token)['order']['status'] == 'success'
call('GET', f'/payments/status/{order["order_id"]}', token=other_token, status=403)

failed = call('POST', '/payments/vnpay/create', {'type': 'wallet_topup', 'amount': 100000, 'pin': pin}, token, status=201)['order']
before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
assert callback(failed, response='24', transaction='02')['RspCode'] == '00'
assert call('GET', f'/payments/status/{failed["order_id"]}', token=token)['order']['status'] == 'failed'
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before

external_bill = {'type': 'bill_payment', 'bill_type': 'water', 'bill_code': 'QA-VNP-' + stamp, 'amount': 50000, 'pin': pin}
external = call('POST', '/payments/vnpay/create', external_bill, token, status=201)['order']
call('POST', '/transactions/bill-payment', {**external_bill, 'user_id': first}, token, status=409)
before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
assert callback(external)['RspCode'] == '00'
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before, 'External bank payment must not debit the wallet again'
call('POST', '/payments/vnpay/create', external_bill, token, status=409)
call('GET', '/payments/orders', token=token)

# Uploading photos must not claim that biometric verification has happened.
call('PATCH', f'/kyc/update-biometric/{first}', {'has_biometric': True}, token, status=400)
image = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg=='
photos = {'front_image': image, 'back_image': image, 'face_image': image, 'has_biometric': True}
call('PATCH', f'/kyc/update-biometric/{first}', photos, other_token, status=403)
review = call('PATCH', f'/kyc/update-biometric/{first}', photos, token)['kyc']
listed = next(r for r in call('GET', '/admin/kyc/requests', token=admin_token)['requests'] if str(r['verification_id']) == str(suite['kyc']))
assert listed['front_image'] == '' and listed['has_images'], 'KYC list must not carry the base64 photos'
assert call('GET', f"/admin/kyc/detail/{suite['kyc']}", token=admin_token)['request']['front_image'] == image, 'KYC detail returns the photos'
assert review['status'] == 'submitted' and review['photo_review_status'] == 'pending' and not review['has_biometric']
call('POST', f'/admin/kyc/approve/{suite["kyc"]}', token=admin_token)
assert call('GET', f'/kyc/status/{first}', token=token)['kyc']['photo_review_status'] == 'approved'

before = call('GET', f'/wallet/{first}', token=token)['wallet']['balance']
policy = call('POST', '/insurance/create', {'user_id': first, 'type': 'Mô phỏng', 'name': 'QA fees', 'premium': 500000, 'coverage': 10000000, 'term': 2}, token, status=201)['policy']['id']
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before - 1000000
call('POST', '/insurance/renew', {'user_id': first, 'id': policy, 'term': 1}, token)
assert call('GET', f'/wallet/{first}', token=token)['wallet']['balance'] == before - 1500000

# Repeated wrong PIN / password lock the account for 15 minutes (separate QA user).
lock_email = f'qa-wallet-{stamp}-lock@example.test'
lock_phone = '09' + str(int(stamp[-7:]) * 10 + 7).zfill(8)
lock_id = call('POST', '/auth/register', {'name': 'QA Lock', 'email': lock_email, 'phone': lock_phone, 'password': 'QA-Wallet@123'}, status=201)['userId']
lock_token = call('POST', '/auth/login', {'identifier': lock_email, 'password': 'QA-Wallet@123'})['token']
call('POST', '/auth/set-pin', {'userId': lock_id, 'pinCode': '123456'}, lock_token)
wrong_pin = {'from_user_id': lock_id, 'to_phone': other_phone, 'amount': 10000, 'pin': '000000'}
for _ in range(4):
    call('POST', '/transactions/transfer', wrong_pin, lock_token, status=401)
assert call('POST', '/transactions/transfer', wrong_pin, lock_token, status=429)['code'] == 'PIN_LOCKED'
call('POST', '/transactions/transfer', {**wrong_pin, 'pin': '123456'}, lock_token, status=429)
for _ in range(4):
    call('POST', '/auth/login', {'identifier': lock_email, 'password': 'wrong'}, status=401)
assert call('POST', '/auth/login', {'identifier': lock_email, 'password': 'wrong'}, status=429)['code'] == 'LOGIN_LOCKED'
call('POST', '/auth/login', {'identifier': lock_email, 'password': 'QA-Wallet@123'}, status=429)

path = pathlib.Path('docs/wallet-test-results'); path.mkdir(exist_ok=True)
(path / 'extended-smoke.json').write_text(json.dumps({'passed': len(results), 'checks': results, 'provider_callback_mode': 'independently signed local fixtures; not a real VNPay bank transaction'}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'PASS: {len(results)} HTTP checks; VNPay success/failure/signature/amount/replay/ownership, profile and idempotency verified.')
