import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { ThemeSwitcher } from "../components/ThemeSwitcher";
import { useConfirm } from "../confirm/useConfirm";
import { t, type StringKey } from "../i18n/t";
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
      <DeleteAccountSection />
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
  const { me } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  /** The new address a confirmation link was sent to. */
  const [sentTo, setSentTo] = useState<string>();
  const { run, busy, error } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const newEmail = email.trim();
    if (await run(() => api.changeEmail(newEmail, password))) {
      setSentTo(newEmail);
      setEmail("");
      setPassword("");
    }
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("settings.emailTitle")}</h2>
      <p className="muted">{t("settings.emailHelp")}</p>
      <p>{t("settings.currentEmail", { email: me?.email ?? "" })}</p>
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
      {sentTo && <p className="ok">{t("settings.emailSent", { email: sentTo })}</p>}
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

function DeleteAccountSection() {
  const { setMe } = useAuth();
  const confirm = useConfirm();
  const [password, setPassword] = useState("");
  const { run, busy, error } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => {
      const deletion = await api.accountDeletion();
      const confirmed = await confirm({
        title: t("settings.deleteConfirmTitle"),
        message: deletion.isLastParent
          ? t("settings.deleteLastParent", {
              children: countText(
                deletion.children,
                "settings.deleteNoChildren",
                "settings.deleteOneChild",
                "settings.deleteChildren",
              ),
              devices: countText(
                deletion.devices,
                "settings.deleteNoDevices",
                "settings.deleteOneDevice",
                "settings.deleteDevices",
              ),
            })
          : t("settings.deleteOtherParents"),
        confirmLabel: t("settings.deleteSubmit"),
        danger: true,
      });
      if (!confirmed) return;
      await api.deleteAccount(password);
      setMe(null);
    });
  };

  return (
    <form className="card stack danger-zone" onSubmit={submit}>
      <h2>{t("settings.deleteTitle")}</h2>
      <p className="muted">{t("settings.deleteHelp")}</p>
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
        <button className="danger" disabled={busy}>
          {t("settings.deleteSubmit")}
        </button>
      </div>
      <ErrorText error={error} />
    </form>
  );
}

function countText(count: number, none: StringKey, one: StringKey, many: StringKey) {
  return t(count === 0 ? none : count === 1 ? one : many, { count });
}
