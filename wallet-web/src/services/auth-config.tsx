import { createContext, useContext } from 'react';

export interface AuthConfiguration {
  googleClientId: string;
  vnpayEnabled: boolean;
  developmentReset: boolean;
}

export const AuthConfigurationContext = createContext<AuthConfiguration>({ googleClientId: '', vnpayEnabled: false, developmentReset: false });
export const useAuthConfiguration = () => useContext(AuthConfigurationContext);
