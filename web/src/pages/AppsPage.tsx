import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import {
  matcherKinds,
  type App,
  type Identifier,
  type MatcherKind,
  type UnassignedIdentifier,
} from "../api/types";
import { AppPicker } from "../components/AppPicker";
import { ErrorText } from "../components/ErrorText";
import { appSuggestionEmail } from "../config";
import { useConfirm } from "../confirm/useConfirm";
import { formatDate, formatMinutes } from "../format";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";

export function AppsPage() {
  const apps = useLoad(api.apps, "apps");
  const unassigned = useLoad(api.unassigned, "unassigned");
  const [error, setError] = useState<string>();

  const run = async (action: () => Promise<unknown>) => {
    setError(undefined);
    try {
      await action();
      await Promise.all([apps.reload(), unassigned.reload()]);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  return (
    <div className="stack-lg">
      <h1>{t("apps.title")}</h1>
      <p className="muted">{t("apps.help")}</p>
      <ErrorText error={error ?? apps.error ?? unassigned.error} />

      <Unassigned items={unassigned.data ?? []} apps={apps.data ?? []} run={run} />

      <AddApp run={run} />

      {apps.data?.map((app) => (
        <AppCard key={app.id} app={app} run={run} />
      ))}
    </div>
  );
}

type Run = (action: () => Promise<unknown>) => Promise<void>;

/** Creates a family app and assigns the identifier to it; removes the app again if assigning fails. */
async function createAndAssign(name: string, item: UnassignedIdentifier) {
  const app = await api.createApp(name);
  try {
    await api.addIdentifier(app.id, item.kind, item.rawIdentifier);
  } catch (err) {
    await api.deleteApp(app.id).catch(() => undefined);
    throw err;
  }
}

/**
 * A mailto: link suggesting a family's identifier for the built-in catalog, or undefined when suggestions
 * are switched off. iOS tokens differ per device, so they're never worth suggesting.
 */
function suggestionLink(appName: string, identifier: Identifier): string | undefined {
  if (!appSuggestionEmail || identifier.kind === "IosToken") return undefined;
  const subject = t("apps.suggestSubject", { app: appName });
  const body = t("apps.suggestBody", {
    app: appName,
    kind: t(`kind.${identifier.kind}`),
    value: identifier.value,
  });
  return `mailto:${appSuggestionEmail}?subject=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`;
}

function Unassigned({
  items,
  apps,
  run,
}: {
  items: UnassignedIdentifier[];
  apps: App[];
  run: Run;
}) {
  return (
    <section className="card stack">
      <h2>{t("apps.unassignedTitle")}</h2>
      <p className="muted">{t("apps.unassignedHelp")}</p>
      {items.length === 0 ? (
        <p className="muted">{t("apps.unassignedEmpty")}</p>
      ) : (
        <ul className="rows">
          {items.map((item) => (
            <li key={`${item.kind}:${item.rawIdentifier}`}>
              <div className="row-main">
                <strong className="mono">{item.rawIdentifier}</strong>
                <span className="muted small">
                  {t(`kind.${item.kind}`)} ·{" "}
                  {t("apps.unassignedLine", {
                    minutes: formatMinutes(item.minutesLast7Days),
                    date: formatDate(item.lastSeenUtc),
                  })}
                </span>
              </div>
              <AppPicker
                apps={apps}
                label={t("apps.assignTo")}
                pickLabel={(app) => t("apps.assignToApp", { app: app.name })}
                onPick={(app) =>
                  run(() => api.addIdentifier(app.id, item.kind, item.rawIdentifier))
                }
                createLabel={(name) => t("apps.createAndAssign", { name })}
                onCreate={(name) => run(() => createAndAssign(name, item))}
              />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function AddApp({ run }: { run: Run }) {
  const [name, setName] = useState("");
  const submit = async (e: FormEvent) => {
    e.preventDefault();
    await run(() => api.createApp(name.trim()));
    setName("");
  };
  return (
    <form className="card inline" onSubmit={submit}>
      <label className="grow">
        {t("apps.addApp")}
        <input
          required
          maxLength={100}
          placeholder={t("apps.appName")}
          value={name}
          onChange={(e) => setName(e.target.value)}
        />
      </label>
      <button className="primary">{t("common.add")}</button>
    </form>
  );
}

function AppCard({ app, run }: { app: App; run: Run }) {
  const [kind, setKind] = useState<MatcherKind>("Process");
  const [value, setValue] = useState("");
  const confirm = useConfirm();

  const add = async (e: FormEvent) => {
    e.preventDefault();
    await run(() => api.addIdentifier(app.id, kind, value.trim()));
    setValue("");
  };

  const remove = async () => {
    const confirmed = await confirm({
      title: t("apps.deleteTitle", { name: app.name }),
      message: t("apps.deleteConfirm"),
      confirmLabel: t("apps.deleteApp"),
      danger: true,
    });
    if (confirmed) await run(() => api.deleteApp(app.id));
  };

  return (
    <section className="card stack">
      <div className="page-header">
        <h2>{app.name}</h2>
        <span className="badge">{app.isBuiltIn ? t("apps.builtIn") : t("apps.custom")}</span>
      </div>
      <div className="chips">
        {app.identifiers.map((id) => (
          <span key={id.id} className="chip" title={t(`kind.${id.kind}`)}>
            <span className="muted small">{t(`kind.${id.kind}`)}</span>{" "}
            <span className="mono">{id.value}</span>
            {!id.isBuiltIn && suggestionLink(app.name, id) && (
              <a
                className="chip-suggest small"
                href={suggestionLink(app.name, id)}
                title={t("apps.suggestTitle")}
              >
                {t("apps.suggest")}
              </a>
            )}
            {!id.isBuiltIn && (
              <button
                type="button"
                className="chip-remove"
                aria-label={t("rules.remove")}
                onClick={() => void run(() => api.removeIdentifier(app.id, id.id))}
              >
                ×
              </button>
            )}
          </span>
        ))}
      </div>
      <form className="inline" onSubmit={add}>
        <select
          value={kind}
          onChange={(e) => setKind(e.target.value as MatcherKind)}
          aria-label={t("apps.identifiers")}
        >
          {matcherKinds.map((k) => (
            <option key={k} value={k}>
              {t(`kind.${k}`)}
            </option>
          ))}
        </select>
        <input
          required
          maxLength={255}
          placeholder={t("apps.identifierValue")}
          value={value}
          onChange={(e) => setValue(e.target.value)}
          className="grow"
        />
        <button>{t("apps.addIdentifier")}</button>
        {!app.isBuiltIn && (
          <button type="button" className="link danger" onClick={() => void remove()}>
            {t("apps.deleteApp")}
          </button>
        )}
      </form>
    </section>
  );
}
