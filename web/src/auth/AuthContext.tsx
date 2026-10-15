import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, setUnauthorizedHandler } from "../api/client";
import type { Me, TwoFactorMethod } from "../api/types";

export interface Auth {
  /** undefined while checking the session, null when signed out. */
  me: Me | null | undefined;
  /** Checks the password. Signs in on a remembered browser; otherwise resolves to the second step that's needed. */
  login: (email: string, password: string) => Promise<TwoFactorMethod | null>;
  logout: () => Promise<void>;
  /** Updates the signed-in parent after an account change (name or email), or null once the account is deleted. */
  setMe: (me: Me | null) => void;
}

export const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>();

  useEffect(() => {
    setUnauthorizedHandler(() => setMe(null));
    api.me().then(setMe, () => setMe(null));
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    const response = await api.login(email, password);
    if (response.me) setMe(response.me);
    return response.secondStep;
  }, []);
  const logout = useCallback(async () => {
    await api.logout();
    setMe(null);
  }, []);

  const value = useMemo(() => ({ me, login, logout, setMe }), [me, login, logout]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
