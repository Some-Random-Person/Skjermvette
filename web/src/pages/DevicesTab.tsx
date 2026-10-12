import { useState, type FormEvent } from "react";
import { api } from "../api/client";
import type { Device, PairingCode } from "../api/types";
import { ErrorText } from "../components/ErrorText";
import { useConfirm } from "../confirm/useConfirm";
import { formatDate, formatMinutes, formatTime } from "../format";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";
import { useChild } from "./ChildPage";

export function DevicesTab() {
  const { child } = useChild();
  const { data: devices, error, reload } = useLoad(() => api.devices(child.id), child.id, 15_000);

  return (
    <div className="stack-lg">
      <Pairing childId={child.id} childName={child.name} />
      <section className="card stack">
        <h2>{t("devices.title")}</h2>
        <ErrorText error={error} />
        {devices?.length === 0 && <p className="muted">{t("devices.empty")}</p>}
        {devices?.map((device) => (
          <DeviceSettings
            key={device.id}
            device={device}
            childName={child.name}
            childAllowance={child.offlineAllowanceMinutes}
            onChanged={reload}
          />
        ))}
      </section>
    </div>
  );
}

function Pairing({ childId, childName }: { childId: string; childName: string }) {
  const [code, setCode] = useState<PairingCode>();
  const [error, setError] = useState<string>();

  const create = async () => {
    setError(undefined);
    try {
      setCode(await api.pairingCode(childId));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  return (
    <section className="card stack">
      <h2>{t("devices.pairTitle")}</h2>
      <p className="muted">{t("devices.pairHelp", { name: childName })}</p>
      {code && (
        <div className="pairing-code">
          <span className="muted small">{t("devices.pairCode")}</span>
          <span className="code">{code.code}</span>
          <span className="muted small">
            {t("devices.pairExpires", { time: formatTime(code.expiresAtUtc) })}
          </span>
        </div>
      )}
      <div>
        <button className="primary" onClick={() => void create()}>
          {t("devices.pairCreate")}
        </button>
      </div>
      <ErrorText error={error} />
    </section>
  );
}

interface DeviceSettingsProps {
  device: Device;
  childName: string;
  childAllowance: number;
  onChanged: () => Promise<void>;
}

function DeviceSettings({ device, childName, childAllowance, onChanged }: DeviceSettingsProps) {
  const [name, setName] = useState(device.name);
  const [override, setOverride] = useState<number | null>(device.offlineAllowanceOverrideMinutes);
  const [error, setError] = useState<string>();
  const [saved, setSaved] = useState(false);
  const confirm = useConfirm();

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setError(undefined);
    try {
      await api.updateDevice(device.id, name, override);
      setSaved(true);
      await onChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  const unpair = async () => {
    const confirmed = await confirm({
      title: t("devices.unpairTitle", { device: device.name }),
      message: t("devices.unpairConfirm"),
      confirmLabel: t("devices.unpair"),
      danger: true,
    });
    if (!confirmed) return;
    try {
      await api.unpairDevice(device.id);
      await onChanged();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  const changed = () => setSaved(false);

  return (
    <form className="device stack" onSubmit={save}>
      <div className="row-main">
        <strong>{device.name}</strong>
        <span className="muted small">
          {t(`platform.${device.platform}`)} ·{" "}
          {t("devices.paired", { date: formatDate(device.pairedAtUtc) })}
          {" · "}
          {device.isOnline
            ? t("today.online")
            : device.lastSeenAtUtc
              ? t("today.offlineSince", { time: formatTime(device.lastSeenAtUtc) })
              : t("today.neverSeen")}
        </span>
      </div>
      <div className="two-col">
        <label>
          {t("devices.rename")}
          <input
            required
            maxLength={100}
            value={name}
            onChange={(e) => {
              setName(e.target.value);
              changed();
            }}
          />
        </label>
        <label>
          {t("devices.allowance")}
          <select
            value={override === null ? "default" : "custom"}
            onChange={(e) => {
              setOverride(e.target.value === "default" ? null : childAllowance);
              changed();
            }}
          >
            <option value="default">
              {t("devices.allowanceDefault", {
                name: childName,
                minutes: formatMinutes(childAllowance),
              })}
            </option>
            <option value="custom">{t("devices.allowanceCustom")}</option>
          </select>
        </label>
      </div>
      {override !== null && (
        <div className="inline">
          <input
            type="number"
            min={0}
            max={60}
            value={override}
            aria-label={t("devices.allowance")}
            onChange={(e) => {
              setOverride(Number(e.target.value));
              changed();
            }}
          />
          <span>{t("common.minutes")}</span>
        </div>
      )}
      <span className="hint">{t("devices.allowanceHelp")}</span>
      <div className="inline">
        <button className="primary">{t("common.save")}</button>
        {saved && <span className="ok">{t("common.saved")}</span>}
        <button type="button" className="link danger" onClick={() => void unpair()}>
          {t("devices.unpair")}
        </button>
      </div>
      <ErrorText error={error} />
    </form>
  );
}
