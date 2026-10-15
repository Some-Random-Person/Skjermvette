import { Outlet } from "react-router";
import { t } from "../i18n/t";
import { ThemeSwitcher } from "./ThemeSwitcher";

/** Frame for the pages a signed-out parent sees: sign in, and the pages linked from emails. */
export function PublicLayout() {
  return (
    <main className="page narrow">
      <h1 className="brand-large">{t("app.name")}</h1>
      <Outlet />
      <div className="login-footer">
        <ThemeSwitcher />
      </div>
    </main>
  );
}
