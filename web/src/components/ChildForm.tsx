import { useMemo, useState, type FormEvent } from "react";
import { api } from "../api/client";
import type { AppRule, AppRuleKind, ChildRequest } from "../api/types";
import { formatMinutes } from "../format";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";
import { AppPicker } from "./AppPicker";
import { ErrorText } from "./ErrorText";
import { OfflineAllowanceHelp } from "./OfflineAllowanceHelp";

const timeZones: string[] = Intl.supportedValuesOf("timeZone");

export const newChild: ChildRequest = {
  name: "",
  timeZoneId: Intl.DateTimeFormat().resolvedOptions().timeZone,
  resetHour: 0,
  dailyTotalMinutes: 120,
  offlineAllowanceMinutes: 5,
  rules: [],
};

interface Props {
  initial: ChildRequest;
  submitLabel: string;
  onSubmit: (child: ChildRequest) => Promise<void>;
}

/** Daily total, time zone, app rules and offline allowance. Used to add a child and on the Rules tab. */
export function ChildForm({ initial, submitLabel, onSubmit }: Props) {
  const [form, setForm] = useState<ChildRequest>(initial);
  const [error, setError] = useState<string>();
  const [status, setStatus] = useState<"idle" | "saving" | "saved">("idle");
  const { data: apps } = useLoad(api.apps, "apps");

  const update = (changes: Partial<ChildRequest>) => {
    setForm((f) => ({ ...f, ...changes }));
    setStatus("idle");
  };
  const updateRule = (index: number, changes: Partial<AppRule>) =>
    update({ rules: form.rules.map((r, i) => (i === index ? { ...r, ...changes } : r)) });

  const unusedApps = useMemo(
    () => (apps ?? []).filter((a) => !form.rules.some((r) => r.appId === a.id)),
    [apps, form.rules],
  );
  const appName = (id: string) => apps?.find((a) => a.id === id)?.name ?? "…";

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setStatus("saving");
    setError(undefined);
    try {
      await onSubmit(form);
      setStatus("saved");
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      setStatus("idle");
    }
  };

  const hours = Math.floor(form.dailyTotalMinutes / 60);
  const minutes = form.dailyTotalMinutes % 60;

  return (
    <form className="stack-lg" onSubmit={submit}>
      <section className="card stack">
        <label>
          {t("rules.name")}
          <input
            required
            maxLength={100}
            value={form.name}
            onChange={(e) => update({ name: e.target.value })}
          />
        </label>

        <fieldset>
          <legend>{t("rules.dailyTotal")}</legend>
          <div className="inline">
            <input
              type="number"
              min={0}
              max={24}
              value={hours}
              aria-label={t("rules.hoursShort")}
              onChange={(e) => update({ dailyTotalMinutes: Number(e.target.value) * 60 + minutes })}
            />
            <span>{t("rules.hoursShort")}</span>
            <input
              type="number"
              min={0}
              max={59}
              step={5}
              value={minutes}
              aria-label={t("rules.minutesShort")}
              onChange={(e) => update({ dailyTotalMinutes: hours * 60 + Number(e.target.value) })}
            />
            <span>{t("rules.minutesShort")}</span>
          </div>
          <span className="hint">{t("rules.dailyTotalHelp")}</span>
        </fieldset>

        <div className="two-col">
          <label>
            {t("rules.timeZone")}
            <select
              value={form.timeZoneId}
              onChange={(e) => update({ timeZoneId: e.target.value })}
            >
              {timeZones.map((zone) => (
                <option key={zone}>{zone}</option>
              ))}
            </select>
          </label>
          <label>
            {t("rules.resetHour")}
            <select
              value={form.resetHour}
              onChange={(e) => update({ resetHour: Number(e.target.value) })}
            >
              {Array.from({ length: 24 }, (_, h) => (
                <option key={h} value={h}>
                  {String(h).padStart(2, "0")}:00
                </option>
              ))}
            </select>
            <span className="hint">{t("rules.resetHourHelp")}</span>
          </label>
        </div>
      </section>

      <section className="card stack">
        <h2>{t("rules.appsTitle")}</h2>
        <p className="muted">{t("rules.appsHelp")}</p>
        {form.rules.length === 0 && <p className="muted">{t("rules.noRules")}</p>}
        <ul className="rows">
          {form.rules.map((rule, i) => (
            <li key={rule.appId}>
              <strong className="row-main">{appName(rule.appId)}</strong>
              <div className="inline">
                <select
                  value={rule.kind}
                  aria-label={appName(rule.appId)}
                  onChange={(e) => {
                    const kind = e.target.value as AppRuleKind;
                    updateRule(i, {
                      kind,
                      limitMinutes: kind === "Limited" ? (rule.limitMinutes ?? 30) : null,
                    });
                  }}
                >
                  <option value="Limited">{t("rules.kind.Limited")}</option>
                  <option value="Exempt">{t("rules.kind.Exempt")}</option>
                </select>
                {rule.kind === "Limited" && (
                  <>
                    <input
                      type="number"
                      min={0}
                      max={1440}
                      step={5}
                      value={rule.limitMinutes ?? 0}
                      aria-label={t("rules.limit")}
                      onChange={(e) => updateRule(i, { limitMinutes: Number(e.target.value) })}
                    />
                    <span>{t("common.minutes")}</span>
                  </>
                )}
                <button
                  type="button"
                  className="link danger"
                  onClick={() => update({ rules: form.rules.filter((_, j) => j !== i) })}
                >
                  {t("rules.remove")}
                </button>
              </div>
            </li>
          ))}
        </ul>
        {unusedApps.length > 0 && (
          <label>
            {t("rules.addRule")}
            <AppPicker
              apps={unusedApps}
              label={t("rules.addRule")}
              pickLabel={(app) => t("rules.addRuleFor", { app: app.name })}
              onPick={(app) =>
                update({
                  rules: [...form.rules, { appId: app.id, kind: "Limited", limitMinutes: 30 }],
                })
              }
            />
          </label>
        )}
      </section>

      <section className="card stack">
        <h2>{t("offline.title")}</h2>
        <div className="inline">
          <input
            type="number"
            min={0}
            max={60}
            value={form.offlineAllowanceMinutes}
            aria-label={t("offline.title")}
            onChange={(e) => update({ offlineAllowanceMinutes: Number(e.target.value) })}
          />
          <span>{t("common.minutes")}</span>
          <span className="muted">({formatMinutes(form.offlineAllowanceMinutes)})</span>
        </div>
        <span className="hint">{t("offline.range")}</span>
        <OfflineAllowanceHelp minutes={form.offlineAllowanceMinutes} childName={form.name} />
      </section>

      <div className="inline">
        <button className="primary" disabled={status === "saving"}>
          {status === "saving" ? t("common.saving") : submitLabel}
        </button>
        {status === "saved" && <span className="ok">{t("common.saved")}</span>}
      </div>
      <ErrorText error={error} />
    </form>
  );
}
