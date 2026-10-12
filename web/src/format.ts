import { t } from "./i18n/t";

/** "45 min", "2 h", "1 h 30 min". Rounds to whole minutes. */
export function formatMinutes(minutes: number): string {
  const total = Math.max(0, Math.round(minutes));
  const h = Math.floor(total / 60);
  const m = total % 60;
  if (h === 0) return t("duration.minutes", { m });
  if (m === 0) return t("duration.hours", { h });
  return t("duration.hoursMinutes", { h, m });
}

export function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" });
}

export function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, { day: "numeric", month: "short" });
}
