import { useNavigate } from "react-router";
import { api } from "../api/client";
import { ChildForm } from "../components/ChildForm";
import { t } from "../i18n/t";
import { useChild } from "./ChildPage";

export function RulesTab() {
  const { child, reloadChild } = useChild();
  const navigate = useNavigate();

  const remove = async () => {
    if (!confirm(t("rules.deleteConfirm", { name: child.name }))) return;
    await api.deleteChild(child.id);
    navigate("/");
  };

  return (
    <div className="stack-lg">
      <ChildForm
        key={child.id}
        initial={child}
        submitLabel={t("common.save")}
        onSubmit={async (request) => {
          await api.updateChild(child.id, request);
          await reloadChild();
        }}
      />
      <div>
        <button className="danger" onClick={() => void remove()}>
          {t("rules.deleteChild")}
        </button>
      </div>
    </div>
  );
}
