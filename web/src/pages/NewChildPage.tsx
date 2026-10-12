import { Link, useNavigate } from "react-router";
import { api } from "../api/client";
import { ChildForm, newChild } from "../components/ChildForm";
import { t } from "../i18n/t";

export function NewChildPage() {
  const navigate = useNavigate();
  return (
    <>
      <Link to="/" className="back">
        ← {t("child.back")}
      </Link>
      <h1>{t("rules.newTitle")}</h1>
      <ChildForm
        initial={newChild}
        submitLabel={t("rules.create")}
        onSubmit={async (request) => {
          const child = await api.createChild(request);
          navigate(`/children/${child.id}/devices`);
        }}
      />
    </>
  );
}
