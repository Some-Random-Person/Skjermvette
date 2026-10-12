import { formatMinutes } from "../format";
import { t } from "../i18n/t";

/** Plain-language explanation of the offline allowance, with a worked example and the trade-off. */
export function OfflineAllowanceHelp({
  minutes,
  childName,
}: {
  minutes: number;
  childName: string;
}) {
  return (
    <div className="help">
      <p>
        {minutes === 0
          ? t("offline.summaryZero")
          : t("offline.summary", {
              minutes: formatMinutes(minutes),
              name: childName || t("offline.yourChild"),
            })}
      </p>
      <details>
        <summary>{t("offline.howTitle")}</summary>
        <p>{t("offline.how1")}</p>
        <p>{t("offline.how2")}</p>
        <h4>{t("offline.exampleTitle")}</h4>
        <p>{t("offline.example")}</p>
        <h4>{t("offline.tradeoffTitle")}</h4>
        <ul>
          <li>{t("offline.tradeoffHigh")}</li>
          <li>{t("offline.tradeoffLow")}</li>
        </ul>
      </details>
    </div>
  );
}
