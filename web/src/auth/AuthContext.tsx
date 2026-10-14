import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { api, setUnauthorizedHandler } from "../api/client";
import type { Me } from "../api/types";

export interface Auth {
  /** undefined while checking the session, null when signed out. */
  me: Me | null | undefined;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  /** Updates the signed-in parent after an account change (name or email). */
  setMe: (me: Me) => void;
}

export const AuthContext = createContext<Auth | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>();

  useEffect(() => {
    setUnauthorizedHandler(() => setMe(null));
    api.me().then(setMe, () => setMe(null));
  }, []);

  const login = useCallback(async (email: string, password: string) => {
    setMe(await api.login(email, password));
  }, []);
  const register = useCallback(async (email: string, password: string) => {
    setMe(await api.register(email, password));
  }, []);
  const logout = useCallback(async () => {
    await api.logout();
    setMe(null);
  }, []);

  const value = useMemo(
    () => ({ me, login, register, logout, setMe }),
    [me, login, register, logout],
  );
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
