import { NavLink, Outlet } from "react-router";
import { useAuth } from "../auth/useAuth";
import { t } from "../i18n/t";

export function Layout() {
  const { me, logout } = useAuth();

  return (
    <>
      <header className="topbar">
        <div className="topbar-inner">
          <NavLink to="/" className="brand">
            {t("app.name")}
          </NavLink>
          <nav>
            <NavLink to="/" end>
              {t("nav.children")}
            </NavLink>
            <NavLink to="/apps">{t("nav.apps")}</NavLink>
            <NavLink to="/settings">{t("nav.settings")}</NavLink>
          </nav>
          <div className="topbar-user">
            <span className="muted">{me?.name || me?.email}</span>
            <button className="link" onClick={() => void logout()}>
              {t("nav.signOut")}
            </button>
          </div>
        </div>
      </header>
      <main className="page">
        <Outlet />
      </main>
    </>
  );
}
