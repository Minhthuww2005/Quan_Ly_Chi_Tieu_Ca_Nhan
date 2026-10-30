const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const ts = require('../wallet-web/node_modules/typescript');
const source = ts.transpileModule(fs.readFileSync(path.join(__dirname, '../wallet-web/src/services/transport.ts'), 'utf8'), { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true } }).outputText;
function harness(fetchImpl) {
  const values = new Map([['token', 'test-token'], ['userInfo', '{}'], ['isLoggedIn', 'true']]);
  const redirects = [];
  const axios = { interceptors: { request: { use() {} }, response: { use() {} } } };
  const window = { fetch: fetchImpl, location: { assign: url => redirects.push(url) } };
  const context = { window, localStorage: { getItem: key => values.get(key) || null, removeItem: key => values.delete(key) }, Headers, Request, Response, URL, Map, Date, crypto: require('node:crypto').webcrypto, exports: {}, require: name => { if (name === './api-origin') return { API_ORIGIN: '', apiUrl: path => path }; assert.equal(name, 'axios'); return axios; } };
  vm.runInNewContext(source, context);
  return { window, values, redirects };
}
(async () => {
  const pin = harness(async () => Response.json({ code: 'INVALID_PIN' }, { status: 401 }));
  assert.equal((await pin.window.fetch('/api/payments/vnpay/create', { method: 'POST', body: '{}' })).status, 401);
  assert.equal(pin.values.get('token'), 'test-token'); assert.equal(pin.redirects.length, 0);
  const expired = harness(async () => Response.json({ message: 'Vui lòng đăng nhập' }, { status: 401 }));
  await expired.window.fetch('/api/wallet/1');
  assert.equal(expired.values.has('token'), false); assert.equal(expired.redirects[0], '/?session=expired');
  const keys = []; let attempts = 0;
  const retry = harness(async (_, init) => { keys.push(init.headers.get('Idempotency-Key')); if (++attempts === 1) throw new Error('network timeout'); return Response.json({ success: true }); });
  const request = { method: 'POST', body: '{"amount":10000}' };
  await assert.rejects(retry.window.fetch('/api/transfer/execute', request));
  await retry.window.fetch('/api/transfer/execute', request);
  assert.equal(keys[0], keys[1]); assert.ok(keys[0]);
  let calls = 0; let resolve;
  const concurrent = harness(() => { calls++; return new Promise(done => { resolve = done; }); });
  const a = concurrent.window.fetch('/api/transfer/execute', request);
  const b = concurrent.window.fetch('/api/transfer/execute', request);
  resolve(Response.json({ transaction: 123 }));
  assert.equal((await (await a).json()).transaction, 123);
  assert.equal((await (await b).json()).transaction, 123); assert.equal(calls, 1);
  console.log('PASS: 4 frontend transport scenarios: wrong PIN keeps session; expired session clears; network retry reuses key; concurrent submission sends once.');
})().catch(error => { console.error(error); process.exitCode = 1; });
