import { useState, type FormEvent } from "react";
import { Navigate } from "react-router";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";

export function LoginPage() {
  const { me, login, register } = useAuth();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  if (me) return <Navigate to="/" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await (mode === "login" ? login(email, password) : register(email, password));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  const isLogin = mode === "login";
  return (
    <main className="page narrow">
      <h1 className="brand-large">{t("app.name")}</h1>
      <form className="card stack" onSubmit={submit}>
        <h2>{isLogin ? t("login.title") : t("login.registerTitle")}</h2>
        <label>
          {t("login.email")}
          <input
            type="email"
            autoComplete="email"
            required
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label>
          {t("login.password")}
          <input
            type="password"
            autoComplete={isLogin ? "current-password" : "new-password"}
            required
            minLength={isLogin ? undefined : 10}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
          />
          {!isLogin && <span className="hint">{t("login.passwordHint")}</span>}
        </label>
        <ErrorText error={error} />
        <button className="primary" disabled={busy}>
          {isLogin ? t("login.submit") : t("login.register")}
        </button>
        <button
          type="button"
          className="link"
          onClick={() => {
            setMode(isLogin ? "register" : "login");
            setError(undefined);
          }}
        >
          {isLogin ? t("login.toRegister") : t("login.toLogin")}
        </button>
      </form>
    </main>
  );
}
