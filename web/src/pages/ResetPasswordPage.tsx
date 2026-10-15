import { useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router";
import { api } from "../api/client";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useAction } from "../useAction";

/** Opened from the link in the password reset email. */
export function ResetPasswordPage() {
  const [params] = useSearchParams();
  const userId = params.get("userId");
  const token = params.get("token");
  const [next, setNext] = useState("");
  const [repeat, setRepeat] = useState("");
  const { run, busy, error, done, setError } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (next !== repeat) {
      setError(t("settings.passwordMismatch"));
      return;
    }
    await run(() => api.resetPassword(userId ?? "", token ?? "", next));
  };

  if (done) {
    return (
      <section className="card stack">
        <h2>{t("reset.title")}</h2>
        <p className="ok">{t("reset.done")}</p>
        <Link to="/login">{t("login.backToLogin")}</Link>
      </section>
    );
  }

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("reset.title")}</h2>
      {!userId || !token ? (
        <ErrorText error={t("link.invalid")} />
      ) : (
        <>
          <label>
            {t("settings.newPassword")}
            <input
              type="password"
              required
              minLength={10}
              autoComplete="new-password"
              value={next}
              onChange={(e) => setNext(e.target.value)}
            />
            <span className="hint">{t("login.passwordHint")}</span>
          </label>
          <label>
            {t("settings.repeatPassword")}
            <input
              type="password"
              required
              minLength={10}
              autoComplete="new-password"
              value={repeat}
              onChange={(e) => setRepeat(e.target.value)}
            />
          </label>
          <ErrorText error={error} />
          <button className="primary" disabled={busy}>
            {t("reset.submit")}
          </button>
        </>
      )}
      <Link to="/forgot-password">{t("reset.newLink")}</Link>
    </form>
  );
}
