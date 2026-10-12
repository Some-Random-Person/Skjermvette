import { Link, NavLink, Outlet, useOutletContext, useParams } from "react-router";
import { api } from "../api/client";
import type { Child } from "../api/types";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";

interface ChildContext {
  child: Child;
  reloadChild: () => Promise<void>;
}

/** Use inside the child page's tabs to get the loaded child. */
export function useChild(): ChildContext {
  return useOutletContext<ChildContext>();
}

export function ChildPage() {
  const { id = "" } = useParams();
  const { data: child, error, reload } = useLoad(() => api.child(id), id);

  return (
    <>
      <Link to="/" className="back">
        ← {t("child.back")}
      </Link>
      <ErrorText error={error} />
      {child && (
        <>
          <h1>{child.name}</h1>
          <nav className="tabs">
            <NavLink to="today">{t("child.tabs.today")}</NavLink>
            <NavLink to="rules">{t("child.tabs.rules")}</NavLink>
            <NavLink to="devices">{t("child.tabs.devices")}</NavLink>
          </nav>
          <Outlet context={{ child, reloadChild: reload } satisfies ChildContext} />
        </>
      )}
    </>
  );
}
