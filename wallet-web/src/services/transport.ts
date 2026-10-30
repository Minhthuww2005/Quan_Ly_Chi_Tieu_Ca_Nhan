import { apiUrl, API_ORIGIN } from './api-origin';
import axios from 'axios';

// Admin pages and user pages share the same-origin .NET API proxy.
axios.interceptors.request.use((config) => {
  const url = config.url || '';
  if (url.startsWith('/api/')) {
    if (API_ORIGIN) config.baseURL = API_ORIGIN;
    const token = localStorage.getItem(url.startsWith('/api/admin/') ? 'adminToken' : 'token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

const originalFetch = window.fetch.bind(window);
const inFlight = new Map<string, Promise<Response>>();
const retryKeys = new Map<string, { key: string; created: number }>();
function expireSession(url: string, status: number, code?: string) {
  if (status !== 401 || code === 'INVALID_PIN' || url.startsWith('/api/auth/') || url === '/api/admin/login') return;
  const admin = url.startsWith('/api/admin/');
  for (const key of admin ? ['adminToken', 'adminInfo'] : ['token', 'userInfo', 'isLoggedIn']) localStorage.removeItem(key);
  window.location.assign(admin ? '/admin/login' : '/?session=expired');
}
axios.interceptors.response.use(response => response, error => {
  expireSession(error.config?.url || '', error.response?.status, error.response?.data?.code);
  return Promise.reject(error);
});
window.fetch = (input, init) => {
  const url = typeof input === 'string' ? input : input instanceof URL ? input.pathname : input.url;
  if (!url.startsWith('/api/')) return originalFetch(input, init);
  const token = localStorage.getItem(url.startsWith('/api/admin/') ? 'adminToken' : 'token');
  const headers = new Headers(input instanceof Request ? input.headers : undefined);
  new Headers(init?.headers).forEach((value, key) => headers.set(key, value));
  if (token) headers.set('Authorization', `Bearer ${token}`);
  const method = init?.method || (input instanceof Request ? input.method : 'GET');
  const financial = method !== 'GET' && /^\/api\/(transactions|transfer|savings|investments|loans|insurance|payments|bank\/topup)(\/|$)/.test(url);
  const signature = financial && typeof init?.body === 'string' ? `${token}:${url}:${init.body}` : '';
  if (signature) {
    for (const [entry, value] of retryKeys) if (Date.now() - value.created > 30 * 60_000) retryKeys.delete(entry);
    if (inFlight.has(signature)) return inFlight.get(signature)!.then(r => r.clone());
    const retry = retryKeys.get(signature) || { key: crypto.randomUUID(), created: Date.now() };
    retryKeys.set(signature, retry);
    if (!headers.has('Idempotency-Key')) headers.set('Idempotency-Key', retry.key);
  }
  const pending = originalFetch(API_ORIGIN ? (input instanceof Request ? new Request(apiUrl(url), input) : apiUrl(url)) : input, { ...init, headers }).then(async response => {
    const errorBody = response.status === 401 ? await response.clone().json().catch(() => ({})) : {};
    expireSession(url, response.status, errorBody.code);
    if (signature) { inFlight.delete(signature); retryKeys.delete(signature); }
    return response;
  }, error => { if (signature) inFlight.delete(signature); throw error; });
  if (signature) inFlight.set(signature, pending);
  return pending.then(response => response.clone());
};
