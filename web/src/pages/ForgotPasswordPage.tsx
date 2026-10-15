import { useState, type FormEvent } from "react";
import { Link } from "react-router";
import { api } from "../api/client";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useAction } from "../useAction";

export function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [sentTo, setSentTo] = useState<string>();
  const { run, busy, error } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (await run(() => api.forgotPassword(email.trim()))) setSentTo(email.trim());
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("forgot.title")}</h2>
      {sentTo ? (
        <p>{t("forgot.sent", { email: sentTo })}</p>
      ) : (
        <>
          <p className="muted">{t("forgot.help")}</p>
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
          <ErrorText error={error} />
          <button className="primary" disabled={busy}>
            {t("forgot.submit")}
          </button>
        </>
      )}
      <Link to="/login">{t("login.backToLogin")}</Link>
    </form>
  );
}
