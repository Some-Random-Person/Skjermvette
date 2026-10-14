import { useState } from "react";

/** Busy / error / done state for a form that calls the API. `run` resolves to true on success. */
export function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [done, setDone] = useState(false);

  const run = async (action: () => Promise<unknown>): Promise<boolean> => {
    setBusy(true);
    setError(undefined);
    setDone(false);
    try {
      await action();
      setDone(true);
      return true;
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
      return false;
    } finally {
      setBusy(false);
    }
  };

  return { run, busy, error, done, setError, clearDone: () => setDone(false) };
}
