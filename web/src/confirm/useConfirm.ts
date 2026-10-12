import { useContext } from "react";
import { ConfirmContext, type Confirm } from "./ConfirmProvider";

/** `if (await confirm({ ... }))`: shows the in-page confirm dialog and resolves to the answer. */
export function useConfirm(): Confirm {
  const confirm = useContext(ConfirmContext);
  if (!confirm) throw new Error("useConfirm must be used inside ConfirmProvider");
  return confirm;
}
