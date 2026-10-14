import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { ThemeSwitcher } from "../components/ThemeSwitcher";
import { t } from "../i18n/t";
import { useAction } from "../useAction";

export function SettingsPage() {
  return (
    <div className="stack-lg narrow-content">
      <h1>{t("settings.title")}</h1>
      <NameSection />
      <EmailSection />
      <PasswordSection />
      <section className="card stack">
        <h2>{t("settings.appearanceTitle")}</h2>
        <p className="muted">{t("settings.appearanceHelp")}</p>
        <ThemeSwitcher />
      </section>
    </div>
  );
}

function NameSection() {
  const { me, setMe } = useAuth();
  const [name, setName] = useState(me?.name ?? "");
  const { run, busy, error, done, clearDone } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => setMe(await api.updateName(name)));
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("settings.nameTitle")}</h2>
      <p className="muted">{t("settings.nameHelp")}</p>
      <label>
        {t("settings.name")}
        <input
          maxLength={100}
          autoComplete="name"
          value={name}
          onChange={(e) => {
            setName(e.target.value);
            clearDone();
          }}
        />
      </label>
      <div className="inline">
        <button className="primary" disabled={busy}>
          {t("common.save")}
        </button>
        {done && <span className="ok">{t("common.saved")}</span>}
      </div>
      <ErrorText error={error} />
    </form>
  );
}

function EmailSection() {
  const { me, setMe } = useAuth();
  const [email, setEmail] = useState("");
  const [repeat, setRepeat] = useState("");
  const [password, setPassword] = useState("");
  const { run, busy, error, done, setError } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (email.trim().toLowerCase() !== repeat.trim().toLowerCase()) {
      setError(t("settings.emailMismatch"));
      return;
    }
    const ok = await run(async () => setMe(await api.changeEmail(email.trim(), password)));
    if (ok) {
      setEmail("");
      setRepeat("");
      setPassword("");
    }
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("settings.emailTitle")}</h2>
      <p className="muted">{t("settings.emailHelp")}</p>
      <p>{t("settings.currentEmail", { email: me?.email ?? "" })}</p>
      <div className="two-col">
        <label>
          {t("settings.newEmail")}
          <input
            type="email"
            required
            autoComplete="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
          />
        </label>
        <label>
          {t("settings.repeatEmail")}
          <input
            type="email"
            required
            autoComplete="email"
            value={repeat}
            onChange={(e) => setRepeat(e.target.value)}
          />
        </label>
      </div>
      <label>
        {t("settings.currentPassword")}
        <input
          type="password"
          required
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />
      </label>
      <div>
        <button className="primary" disabled={busy}>
          {t("settings.emailSubmit")}
        </button>
      </div>
      {done && <p className="ok">{t("settings.emailChanged")}</p>}
      <ErrorText error={error} />
    </form>
  );
}

function PasswordSection() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [repeat, setRepeat] = useState("");
  const { run, busy, error, done, setError } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    if (next !== repeat) {
      setError(t("settings.passwordMismatch"));
      return;
    }
    const ok = await run(() => api.changePassword(current, next));
    if (ok) {
      setCurrent("");
      setNext("");
      setRepeat("");
    }
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("settings.passwordTitle")}</h2>
      <label>
        {t("settings.currentPassword")}
        <input
          type="password"
          required
          autoComplete="current-password"
          value={current}
          onChange={(e) => setCurrent(e.target.value)}
        />
      </label>
      <div className="two-col">
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
      </div>
      <div>
        <button className="primary" disabled={busy}>
          {t("settings.passwordSubmit")}
        </button>
      </div>
      {done && <p className="ok">{t("settings.passwordChanged")}</p>}
      <ErrorText error={error} />
    </form>
  );
}
