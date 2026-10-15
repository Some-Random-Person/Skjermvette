import { Link, useSearchParams } from "react-router";
import { api } from "../api/client";
import { useAuth } from "../auth/useAuth";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";

/** Opened from the link emailed to the new address when a parent changes their email. */
export function ConfirmEmailChangePage() {
  const { me, setMe } = useAuth();
  const [params] = useSearchParams();
  const userId = params.get("userId");
  const email = params.get("email");
  const token = params.get("token");
  const { data: confirmed, error } = useLoad(async () => {
    if (!userId || !email || !token) throw new Error(t("link.invalid"));
    await api.confirmEmailChange(userId, email, token);
    // If this browser is signed in, show the new address straight away.
    api.me().then(setMe, () => {});
    return true;
  }, `${userId}:${email}:${token}`);

  return (
    <section className="card stack">
      <h2>{t("confirmEmailChange.title")}</h2>
      {error ? (
        <>
          <ErrorText error={error} />
          <p className="muted">{t("confirmEmailChange.newLink")}</p>
        </>
      ) : confirmed ? (
        <p className="ok">{t("confirmEmailChange.done", { email: email ?? "" })}</p>
      ) : (
        <p className="muted">{t("common.loading")}</p>
      )}
      {me ? (
        <Link to="/">{t("confirmEmailChange.toDashboard")}</Link>
      ) : (
        <Link to="/login">{t("login.backToLogin")}</Link>
      )}
    </section>
  );
}
