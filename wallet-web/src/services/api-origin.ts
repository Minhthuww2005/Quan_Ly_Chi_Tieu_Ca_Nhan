// Only the public API origin belongs in Vite config. Never put provider/DB secrets here.
const configured = (import.meta.env.VITE_API_ORIGIN || '').replace(/\/$/, '');
if (configured && new URL(configured).protocol !== 'https:' && !['localhost', '127.0.0.1'].includes(new URL(configured).hostname)) {
  throw new Error('VITE_API_ORIGIN must use HTTPS');
}
export const API_ORIGIN = configured;
export const apiUrl = (path: string) => `${API_ORIGIN}${path}`;
