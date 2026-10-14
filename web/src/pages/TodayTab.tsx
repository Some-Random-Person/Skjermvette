import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import type { Device, Today, TodayApp } from "../api/types";
import { ErrorText } from "../components/ErrorText";
import { Meter } from "../components/Meter";
import { formatMinutes, formatTime } from "../format";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";
import { useChild } from "./ChildPage";

export function TodayTab() {
  const { child } = useChild();
  const { data: today, error, reload } = useLoad(() => api.today(child.id), child.id, 15_000);

  if (!today)
    return error ? <ErrorText error={error} /> : <p className="muted">{t("common.loading")}</p>;

  return (
    <div className="stack-lg">
      <section className="card">
        <div className="stats">
          <Stat label={t("today.used")} value={formatMinutes(today.totalUsedMinutes)} />
          <Stat label={t("today.left")} value={formatMinutes(today.totalRemainingMinutes)} />
          <Stat
            label={t("today.limit")}
            value={formatMinutes(today.totalLimitMinutes)}
            note={
              today.extraMinutes > 0
                ? t("today.includesExtra", { extra: formatMinutes(today.extraMinutes) })
                : undefined
            }
          />
        </div>
        <Meter used={today.totalUsedMinutes} total={today.totalLimitMinutes} />
        <p className="muted small">{t("today.updated")}</p>
        <ErrorText error={error} />
      </section>

      <section className="card">
        <h2>{t("today.apps")}</h2>
        {today.apps.length === 0 ? (
          <p className="muted">{t("today.noUsage")}</p>
        ) : (
          <ul className="rows">
            {today.apps.map((app) => (
              <AppRow key={app.appId ?? "other"} app={app} />
            ))}
          </ul>
        )}
        {hasOverlap(today) && <p className="muted small">{t("today.overlapNote")}</p>}
      </section>

      <section className="card">
        <h2>{t("today.devices")}</h2>
        {today.devices.length === 0 ? (
          <p className="muted">{t("today.noDevices")}</p>
        ) : (
          <ul className="rows">
            {today.devices.map((device) => (
              <DeviceRow key={device.id} device={device} />
            ))}
          </ul>
        )}
      </section>

      <ExtraTime childId={child.id} onAdded={reload} />
    </div>
  );
}

/** True when counted apps were used at the same time, so their rows add up to more than "Used today". */
function hasOverlap(today: Today): boolean {
  const counted = today.apps
    .filter((a) => a.kind !== "Exempt")
    .reduce((sum, a) => sum + a.usedMinutes, 0);
  return counted - today.totalUsedMinutes >= 0.5;
}

function Stat({ label, value, note }: { label: string; value: string; note?: string }) {
  return (
    <div className="stat">
      <span className="muted small">{label}</span>
      <span className="big">{value}</span>
      {note && <span className="muted small">{note}</span>}
    </div>
  );
}

function AppRow({ app }: { app: TodayApp }) {
  const name = app.appId === null ? t("today.otherApps") : app.name;
  const used = formatMinutes(app.usedMinutes);

  let detail: string;
  if (app.kind === "Exempt") detail = `${t("today.exempt")} · ${t("today.usedOnly", { used })}`;
  else if (app.remainingMinutes !== null && app.remainingMinutes <= 0)
    detail = `${t("today.blocked")} · ${t("today.usedOnly", { used })}`;
  else detail = t("today.usedLeft", { used, left: formatMinutes(app.remainingMinutes ?? 0) });

  return (
    <li>
      <div className="row-main">
        <strong>{name}</strong>
        <span className="muted small">
          {app.kind === "Limited" && app.limitMinutes !== null
            ? t("today.limited", { limit: formatMinutes(app.limitMinutes) })
            : app.kind === null
              ? t("today.noRule")
              : t("today.exempt")}
        </span>
      </div>
      <div className="row-side">
        <span>{detail}</span>
        {app.kind === "Limited" && app.limitMinutes !== null && (
          <Meter used={app.usedMinutes} total={app.limitMinutes} />
        )}
      </div>
    </li>
  );
}

function DeviceRow({ device }: { device: Device }) {
  const status = device.isOnline
    ? t("today.online")
    : device.lastSeenAtUtc
      ? t("today.offlineSince", { time: formatTime(device.lastSeenAtUtc) })
      : t("today.neverSeen");

  return (
    <li>
      <div className="row-main">
        <strong>{device.name}</strong>
        <span className="muted small">{t(`platform.${device.platform}`)}</span>
      </div>
      <div className="row-side">
        <span>
          <span className={`dot ${device.isOnline ? "dot-on" : "dot-off"}`} aria-hidden /> {status}
          {" · "}
          {t("today.setAside", { minutes: formatMinutes(device.setAsideMinutes) })}
        </span>
      </div>
    </li>
  );
}

function ExtraTime({ childId, onAdded }: { childId: string; onAdded: () => Promise<void> }) {
  const [minutes, setMinutes] = useState(15);
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(undefined);
    try {
      await api.extraTime(childId, minutes);
      await onAdded();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <form className="card stack" onSubmit={submit}>
      <h2>{t("today.extraTitle")}</h2>
      <p className="muted">{t("today.extraHelp")}</p>
      <div className="inline">
        <input
          type="number"
          min={1}
          max={1440}
          value={minutes}
          onChange={(e) => setMinutes(Number(e.target.value))}
          aria-label={t("common.minutes")}
        />
        <span>{t("common.minutes")}</span>
        <button className="primary" disabled={busy || minutes < 1}>
          {t("today.extraSubmit", { minutes })}
        </button>
      </div>
      <ErrorText error={error} />
    </form>
  );
}
