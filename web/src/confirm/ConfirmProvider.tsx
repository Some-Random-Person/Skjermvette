import { createContext, useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { t } from "../i18n/t";

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel: string;
  /** Styles the confirm button as destructive. */
  danger?: boolean;
}

export type Confirm = (options: ConfirmOptions) => Promise<boolean>;

export const ConfirmContext = createContext<Confirm | null>(null);

interface Pending extends ConfirmOptions {
  resolve: (confirmed: boolean) => void;
}

/** Provides `useConfirm()`: an in-page replacement for the browser's `confirm()`. */
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [pending, setPending] = useState<Pending>();
  const dialogRef = useRef<HTMLDialogElement>(null);

  const confirm = useCallback<Confirm>(
    (options) => new Promise((resolve) => setPending({ ...options, resolve })),
    [],
  );

  useEffect(() => {
    if (pending && !dialogRef.current?.open) dialogRef.current?.showModal();
  }, [pending]);

  const finish = (confirmed: boolean) => {
    pending?.resolve(confirmed);
    setPending(undefined);
    dialogRef.current?.close();
  };

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <dialog
        ref={dialogRef}
        className="confirm"
        aria-labelledby="confirm-title"
        aria-describedby="confirm-message"
        // Escape key: treat as cancel.
        onCancel={(e) => {
          e.preventDefault();
          finish(false);
        }}
        // Click on the backdrop (outside the box): cancel.
        onClick={(e) => {
          if (e.target === dialogRef.current) finish(false);
        }}
      >
        {pending && (
          <div className="confirm-box">
            <h2 id="confirm-title">{pending.title}</h2>
            <p id="confirm-message">{pending.message}</p>
            <div className="confirm-actions">
              {/* Cancel gets focus first, so pressing Enter by accident doesn't delete anything. */}
              <button autoFocus onClick={() => finish(false)}>
                {t("common.cancel")}
              </button>
              <button
                className={pending.danger ? "danger-solid" : "primary"}
                onClick={() => finish(true)}
              >
                {pending.confirmLabel}
              </button>
            </div>
          </div>
        )}
      </dialog>
    </ConfirmContext.Provider>
  );
}
