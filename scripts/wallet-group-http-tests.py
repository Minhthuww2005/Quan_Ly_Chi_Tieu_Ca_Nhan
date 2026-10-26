"""Group HTTP regression. Creates QA accounts on a dedicated local database only.
Set WALLET_TEST_BASE if needed. --local-admin-container bootstraps ONLY a QA user
inside the explicitly named local Docker container; never use this on Neon.
"""
import concurrent.futures
import datetime
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

BASE = os.environ.get('WALLET_TEST_BASE', 'http://localhost:5081/api')
assert BASE.startswith(('http://localhost:', 'http://127.0.0.1:')), 'Local QA only'
urllib.request.install_opener(urllib.request.build_opener(urllib.request.ProxyHandler({})))
stamp = str(int(time.time()))
password = 'Group-QA@123456'
checks = []

def call(method, path, body=None, token=None, status=200):
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer ' + token
    req = urllib.request.Request(BASE + path, data=json.dumps(body).encode() if body is not None else None, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=40) as r: code, data = r.status, json.load(r)
    except urllib.error.HTTPError as e:
        code = e.code
        raw = e.read().decode('utf-8')
        data = json.loads(raw) if raw else {}
    assert code == status, f'{method} {path}: {code} != {status}: {data}'
    checks.append({'method': method, 'path': path, 'status': code})
    return data

def signup(n):
    email = f'qa-groups-{stamp}-{n}@example.test'
    phone = '08' + str((int(stamp[-7:]) * 10 + n) % 100000000).zfill(8)
    user = call('POST', '/auth/register', {'name': f'QA Groups {n}', 'email': email, 'phone': phone, 'password': password}, status=201)
    login = call('POST', '/auth/login', {'identifier': email, 'password': password})
    return user['userId'], login['token'], email

call('GET', '/health')
owner, member, outsider, admin = [signup(i) for i in range(1, 5)]
if len(sys.argv) != 3 or sys.argv[1] != '--local-admin-container':
    raise SystemExit('Pass --local-admin-container htdpay-groups-qa-15439 for the isolated local QA database.')
container = sys.argv[2]
assert container == 'htdpay-groups-qa-15439', 'Only the dedicated QA container is allowed'
sql = "UPDATE htdpay.users SET data=jsonb_set(data,'{role}','\"admin\"'::jsonb) WHERE data->>'email'='" + admin[2] + "' AND data->>'name'='QA Groups 4';"
subprocess.run(['docker', 'exec', container, 'psql', '-U', 'htdpayqa', '-d', 'htdpay_groups_qa', '-c', sql], check=True, capture_output=True)
admin = (admin[0], call('POST', '/admin/login', {'email': admin[2], 'password': password})['token'], admin[2])
o, m, x, a = owner[1], member[1], outsider[1], admin[1]
before = call('GET', f'/wallet/{owner[0]}', token=o)['wallet']['balance']
call('GET', '/groups', status=401)
gid = call('POST', '/groups', {'name': 'Nhóm QA HTTP', 'budget': 100000}, o, 201)['id']
path = f'/groups/{gid}'
call('GET', path, token=x, status=404)
call('GET', '/admin/groups', token=o, status=403)
call('POST', path + '/members', {'email': member[2]}, o)
call('POST', path + '/members', {'email': member[2]}, o, 409)
call('POST', path + '/members', {'email': outsider[2]}, m, 403)
date = datetime.datetime.now(datetime.timezone.utc).strftime('%Y-%m-%d')
entry = {'title': 'Ăn trưa', 'amount': 120000, 'kind': 'expense', 'category': 'Ăn uống', 'date': date, 'payer_id': member[0]}
eid = call('POST', path + '/entries', entry, m, 201)['entry']['entry_id']
call('POST', path + '/entries', {**entry, 'title': 'Quỹ chung', 'kind': 'income', 'amount': 200000}, o, 201)
d = call('GET', path, token=o)
assert d['summary']['expense'] == 120000 and d['summary']['income'] == 200000 and d['summary']['overBudget']
call('PUT', path + f'/entries/{eid}', {**entry, 'version': 0}, m, 409)
call('POST', path + '/entries', {**entry, 'amount': -1}, o, 400)
call('POST', path + '/entries', {**entry, 'payer_id': outsider[0]}, o, 400)
call('GET', path + '?month=bad', token=o, status=400)
assert call('GET', path + '?month=2000-01', token=o)['entries'] == []
# Both callers use the same version; exactly one must succeed after the DB lock.
def concurrent_update(n):
    req = urllib.request.Request(BASE + path + f'/entries/{eid}', data=json.dumps({**entry, 'amount': 80000 + n, 'version': 1}).encode(), headers={'Authorization': 'Bearer ' + m, 'Content-Type': 'application/json'}, method='PUT')
    try:
        with urllib.request.urlopen(req, timeout=40) as r: return r.status
    except urllib.error.HTTPError as e: return e.code
with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    statuses = sorted(pool.map(concurrent_update, [0, 1]))
assert statuses == [200, 409], statuses
checks.append({'case': 'concurrent-write', 'statuses': statuses})
call('DELETE', path + f'/members/{member[0]}', token=o)
call('GET', path, token=m, status=404)
call('POST', path + '/members', {'email': member[2]}, o)
ad = call('GET', f'/admin/groups/{gid}', token=a)
call('PATCH', f'/admin/groups/{gid}/status', {'archived': True, 'version': ad['group']['version']}, a)
call('POST', path + '/entries', entry, o, 409)
ad = call('GET', f'/admin/groups/{gid}', token=a)
call('PATCH', f'/admin/groups/{gid}/status', {'archived': False, 'version': ad['group']['version']}, a)
call('DELETE', path + f'/entries/{eid}', {'version': 2}, o)
assert call('GET', path, token=o)['summary']['expense'] == 0
assert call('GET', f'/wallet/{owner[0]}', token=o)['wallet']['balance'] == before
print(json.dumps({'pass': True, 'http_checks': len(checks), 'concurrent_statuses': statuses, 'group_id': gid, 'database': 'isolated local PostgreSQL', 'personal_wallet_unchanged': True}, ensure_ascii=False))
