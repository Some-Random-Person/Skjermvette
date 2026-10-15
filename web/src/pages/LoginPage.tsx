import { useState, type FormEvent } from "react";
import { Link, Navigate } from "react-router";
import { ApiError, api } from "../api/client";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useAction } from "../useAction";

export function LoginPage() {
  const { me, login } = useAuth();
  const [mode, setMode] = useState<"login" | "register">("login");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  /** The address a confirmation link was just sent to, after registering. */
  const [sentTo, setSentTo] = useState<string>();
  /** Signing in failed because the address isn't confirmed yet. */
  const [unconfirmed, setUnconfirmed] = useState(false);

  if (me) return <Navigate to="/" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    setUnconfirmed(false);
    try {
      if (mode === "login") {
        await login(email, password);
      } else {
        await api.register(email, password);
        setSentTo(email);
        setPassword("");
      }
    } catch (err) {
      setUnconfirmed(err instanceof ApiError && err.status === 403);
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  if (sentTo) {
    return (
      <section className="card stack">
        <h2>{t("login.checkEmailTitle")}</h2>
        <p>{t("login.checkEmail", { email: sentTo })}</p>
        <button
          type="button"
          className="link"
          onClick={() => {
            setSentTo(undefined);
            setMode("login");
          }}
        >
          {t("login.backToLogin")}
        </button>
      </section>
    );
  }

  const isLogin = mode === "login";
  return (
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
      {isLogin && <Link to="/forgot-password">{t("login.forgot")}</Link>}
      <ErrorText error={error} />
      {unconfirmed && <ResendConfirmation email={email} />}
      <button className="primary" disabled={busy}>
        {isLogin ? t("login.submit") : t("login.register")}
      </button>
      <button
        type="button"
        className="link"
        onClick={() => {
          setMode(isLogin ? "register" : "login");
          setError(undefined);
          setUnconfirmed(false);
        }}
      >
        {isLogin ? t("login.toRegister") : t("login.toLogin")}
      </button>
    </form>
  );
}

function ResendConfirmation({ email }: { email: string }) {
  const { run, busy, error, done } = useAction();

  return (
    <div className="stack">
      <div className="inline">
        <button
          type="button"
          disabled={busy}
          onClick={() => run(() => api.resendConfirmation(email))}
        >
          {t("login.resend")}
        </button>
        {done && <span className="ok">{t("login.resent")}</span>}
      </div>
      <ErrorText error={error} />
    </div>
  );
}
