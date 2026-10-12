import { Link } from "react-router";
import { api } from "../api/client";
import type { Child, Today } from "../api/types";
import { ErrorText } from "../components/ErrorText";
import { Meter } from "../components/Meter";
import { formatMinutes } from "../format";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";

export function ChildrenPage() {
  const { data, error } = useLoad(
    async () => {
      const children = await api.children();
      const today = await Promise.all(children.map((c) => api.today(c.id)));
      return children.map((child, i) => ({ child, today: today[i] }));
    },
    "children",
    15_000,
  );

  return (
    <>
      <div className="page-header">
        <h1>{t("children.title")}</h1>
        <Link to="/children/new" className="button primary">
          {t("children.add")}
        </Link>
      </div>
      <ErrorText error={error} />
      {!data && !error && <p className="muted">{t("common.loading")}</p>}
      {data?.length === 0 && <p className="card muted">{t("children.empty")}</p>}
      <div className="grid">
        {data?.map(({ child, today }) => (
          <ChildCard key={child.id} child={child} today={today} />
        ))}
      </div>
    </>
  );
}

function ChildCard({ child, today }: { child: Child; today: Today }) {
  const online = today.devices.filter((d) => d.isOnline).length;
  return (
    <Link to={`/children/${child.id}/today`} className="card child-card">
      <h2>{child.name}</h2>
      <Meter used={today.totalUsedMinutes} total={today.totalLimitMinutes} />
      <p>
        {t("children.usedOf", {
          used: formatMinutes(today.totalUsedMinutes),
          total: formatMinutes(today.totalLimitMinutes),
        })}
      </p>
      <p className="big">
        {t("children.left", { left: formatMinutes(today.totalRemainingMinutes) })}
      </p>
      <p className="muted small">
        {today.devices.length === 0
          ? t("today.noDevices")
          : t("children.devicesOnline", { online, count: today.devices.length })}
      </p>
    </Link>
  );
}
