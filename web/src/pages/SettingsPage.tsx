import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import type { AuthenticatorSetup } from "../api/types";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { ThemeSwitcher } from "../components/ThemeSwitcher";
import { useConfirm } from "../confirm/useConfirm";
import { t, type StringKey } from "../i18n/t";
import { useAction } from "../useAction";
import { useLoad } from "../useLoad";

export function SettingsPage() {
  return (
    <div className="stack-lg narrow-content">
      <h1>{t("settings.title")}</h1>
      <NameSection />
      <EmailSection />
      <PasswordSection />
      <SecuritySection />
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

function SecuritySection() {
  const { data, error, reload } = useLoad(api.twoFactor, "two-factor");
  /** Recovery codes to show once, after setting up the app or making new ones. */
  const [codes, setCodes] = useState<string[]>();

  const showCodes = async (recoveryCodes: string[]) => {
    setCodes(recoveryCodes);
    await reload();
  };

  return (
    <section className="card stack">
      <h2>{t("security.title")}</h2>
      <p className="muted">{t("security.help")}</p>
      <ErrorText error={error} />
      {data && (
        <p>
          {data.appEnabled
            ? t("security.statusApp", { count: data.recoveryCodesLeft })
            : t("security.statusEmail")}
        </p>
      )}
      {codes && <RecoveryCodesList codes={codes} onDone={() => setCodes(undefined)} />}
      {data && !data.appEnabled && <SetupApp onEnabled={showCodes} />}
      {data?.appEnabled && !codes && (
        <>
          <NewRecoveryCodes onCreated={showCodes} />
          <RemoveApp onRemoved={reload} />
        </>
      )}
      <ForgetBrowsers />
    </section>
  );
}

function SetupApp({ onEnabled }: { onEnabled: (codes: string[]) => Promise<void> }) {
  const [setup, setSetup] = useState<AuthenticatorSetup>();
  const [code, setCode] = useState("");
  const { run, busy, error } = useAction();

  if (!setup) {
    return (
      <div className="stack">
        <div>
          <button disabled={busy} onClick={() => run(async () => setSetup(await api.setupApp()))}>
            {t("security.setup")}
          </button>
        </div>
        <ErrorText error={error} />
      </div>
    );
  }

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => onEnabled((await api.enableApp(code)).codes));
  };

  return (
    <form className="stack" onSubmit={submit}>
      <h3>{t("security.setupTitle")}</h3>
      <p>{t("security.setupScan")}</p>
      <img
        className="qr"
        src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(setup.qrCodeSvg)}`}
        alt={t("security.qrAlt")}
      />
      <p>
        {t("security.setupKey")} <span className="mono">{setup.key}</span>
      </p>
      <p>
        <a href={setup.uri}>{t("security.setupLink")}</a>
      </p>
      <label>
        {t("security.setupCode")}
        <input
          required
          autoComplete="one-time-code"
          inputMode="numeric"
          value={code}
          onChange={(e) => setCode(e.target.value)}
        />
      </label>
      <div className="inline">
        <button className="primary" disabled={busy}>
          {t("security.setupSubmit")}
        </button>
        <button type="button" className="link" onClick={() => setSetup(undefined)}>
          {t("common.cancel")}
        </button>
      </div>
      <ErrorText error={error} />
    </form>
  );
}

function RecoveryCodesList({ codes, onDone }: { codes: string[]; onDone: () => void }) {
  const [copied, setCopied] = useState(false);

  return (
    <div className="stack">
      <h3>{t("security.codesTitle")}</h3>
      <p>{t("security.codesHelp")}</p>
      <ul className="codes mono">
        {codes.map((code) => (
          <li key={code}>{code}</li>
        ))}
      </ul>
      <div className="inline">
        <button
          type="button"
          onClick={async () => {
            await navigator.clipboard.writeText(codes.join("\n"));
            setCopied(true);
          }}
        >
          {t("security.codesCopy")}
        </button>
        <button type="button" className="primary" onClick={onDone}>
          {t("security.codesDone")}
        </button>
        {copied && <span className="ok">{t("security.codesCopied")}</span>}
      </div>
    </div>
  );
}

function NewRecoveryCodes({ onCreated }: { onCreated: (codes: string[]) => Promise<void> }) {
  const confirm = useConfirm();
  const { run, busy, error } = useAction();

  const create = () =>
    run(async () => {
      const confirmed = await confirm({
        title: t("security.newCodesConfirmTitle"),
        message: t("security.newCodesConfirm"),
        confirmLabel: t("security.newCodes"),
      });
      if (confirmed) await onCreated((await api.newRecoveryCodes()).codes);
    });

  return (
    <div className="stack">
      <div>
        <button type="button" disabled={busy} onClick={create}>
          {t("security.newCodes")}
        </button>
      </div>
      <ErrorText error={error} />
    </div>
  );
}

function RemoveApp({ onRemoved }: { onRemoved: () => Promise<void> }) {
  const [password, setPassword] = useState("");
  const [code, setCode] = useState("");
  const { run, busy, error } = useAction();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(async () => {
      await api.disableApp(password, code);
      await onRemoved();
    });
  };

  return (
    <form className="stack" onSubmit={submit}>
      <h3>{t("security.removeTitle")}</h3>
      <p className="muted">{t("security.removeHelp")}</p>
      <div className="two-col">
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
        <label>
          {t("security.removeCode")}
          <input
            required
            autoComplete="one-time-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
          />
        </label>
      </div>
      <div>
        <button className="danger" disabled={busy}>
          {t("security.removeSubmit")}
        </button>
      </div>
      <ErrorText error={error} />
    </form>
  );
}

function ForgetBrowsers() {
  const confirm = useConfirm();
  const { run, busy, error } = useAction();
  const [forgotten, setForgotten] = useState(false);

  const forget = () =>
    run(async () => {
      const confirmed = await confirm({
        title: t("security.forgetConfirmTitle"),
        message: t("security.forgetConfirm"),
        confirmLabel: t("security.forget"),
      });
      if (!confirmed) return;
      await api.forgetBrowsers();
      setForgotten(true);
    });

  return (
    <div className="stack">
      <h3>{t("security.forgetTitle")}</h3>
      <p className="muted">{t("security.forgetHelp")}</p>
      <div className="inline">
        <button type="button" disabled={busy} onClick={forget}>
          {t("security.forget")}
        </button>
        {forgotten && <span className="ok">{t("security.forgotten")}</span>}
      </div>
      <ErrorText error={error} />
    </div>
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
