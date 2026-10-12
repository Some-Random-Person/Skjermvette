import { useNavigate } from "react-router";
import { api } from "../api/client";
import { ChildForm } from "../components/ChildForm";
import { useConfirm } from "../confirm/useConfirm";
import { t } from "../i18n/t";
import { useChild } from "./ChildPage";

export function RulesTab() {
  const { child, reloadChild } = useChild();
  const navigate = useNavigate();
  const confirm = useConfirm();

  const remove = async () => {
    const confirmed = await confirm({
      title: t("rules.deleteTitle", { name: child.name }),
      message: t("rules.deleteConfirm", { name: child.name }),
      confirmLabel: t("rules.deleteChild"),
      danger: true,
    });
    if (!confirmed) return;
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
