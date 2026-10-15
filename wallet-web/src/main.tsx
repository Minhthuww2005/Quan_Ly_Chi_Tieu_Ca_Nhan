import './services/transport';
import { createRoot } from 'react-dom/client';
import { useEffect, useState } from 'react';
import { GoogleOAuthProvider } from '@react-oauth/google';
import App from './App.tsx';
import './index.css';
import { AuthConfigurationContext, type AuthConfiguration } from './services/auth-config';

function ApplicationBootstrap() {
  const [config, setConfig] = useState<AuthConfiguration | null>(null);
  useEffect(() => {
    fetch('/api/auth/config').then(r => r.json()).then(setConfig)
      .catch(() => setConfig({ googleClientId: '', vnpayEnabled: false, developmentReset: false }));
  }, []);
  if (!config) return <p className="p-8 text-center">Đang kết nối HTD PAY...</p>;
  const app = <AuthConfigurationContext.Provider value={config}><App /></AuthConfigurationContext.Provider>;
  return config.googleClientId ? <GoogleOAuthProvider clientId={config.googleClientId}>{app}</GoogleOAuthProvider> : app;
}

createRoot(document.getElementById('root')!).render(<ApplicationBootstrap />);
