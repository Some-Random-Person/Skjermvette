import { Link, useSearchParams } from "react-router";
import { api } from "../api/client";
import { ErrorText } from "../components/ErrorText";
import { t } from "../i18n/t";
import { useLoad } from "../useLoad";

/** Opened from the link in the confirmation email. */
export function ConfirmEmailPage() {
  const [params] = useSearchParams();
  const userId = params.get("userId");
  const token = params.get("token");
  const { data: confirmed, error } = useLoad(async () => {
    if (!userId || !token) throw new Error(t("link.invalid"));
    await api.confirmEmail(userId, token);
    return true;
  }, `${userId}:${token}`);

  return (
    <section className="card stack">
      <h2>{t("confirmEmail.title")}</h2>
      {error ? (
        <>
          <ErrorText error={error} />
          <p className="muted">{t("confirmEmail.newLink")}</p>
        </>
      ) : confirmed ? (
        <p className="ok">{t("confirmEmail.done")}</p>
      ) : (
        <p className="muted">{t("common.loading")}</p>
      )}
      <Link to="/login">{t("login.backToLogin")}</Link>
    </section>
  );
}
