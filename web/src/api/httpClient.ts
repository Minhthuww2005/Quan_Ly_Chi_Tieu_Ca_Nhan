// TODO(D): Axios instance, token interceptor, error handling.
import axios from 'axios'
import type { Tokens } from './contracts'
export const http = axios.create({ baseURL: '/api', timeout: 15000 })
let tokens: Tokens | null = null
export function setTokens(value: Tokens | null) { tokens = value }
export function accessToken() { return tokens?.accessToken ?? '' }
http.interceptors.request.use(config => { if (tokens) config.headers.Authorization = `Bearer ${tokens.accessToken}`; return config })
let refresh: Promise<Tokens> | null = null
http.interceptors.response.use(r => r, async error => {
  const c = error.config
  if (error.response?.status === 401 && tokens && !c.retried && !c.url?.startsWith('/auth')) {
    c.retried = true
    refresh ??= axios.post<Tokens>('/api/auth/refresh', { refreshToken: tokens.refreshToken }).then(r => r.data).finally(() => { refresh = null })
    try { setTokens(await refresh); return http(c) } catch { setTokens(null) }
  }
  return Promise.reject(error)
})
export function errorMessage(e: unknown) { return axios.isAxiosError(e) ? e.response?.data?.message ?? e.message : String(e) }
