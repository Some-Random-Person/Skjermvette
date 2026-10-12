import { useCallback, useEffect, useRef, useState } from "react";

interface Loaded<T> {
  data: T | undefined;
  error: string | undefined;
  reload: () => Promise<void>;
}

/**
 * Loads data on mount and whenever `key` changes (e.g. the child id), and refreshes every `pollMs` if given.
 * `load` may be an inline function; the latest one is always used.
 */
export function useLoad<T>(load: () => Promise<T>, key: string, pollMs?: number): Loaded<T> {
  const [data, setData] = useState<T>();
  const [error, setError] = useState<string>();
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  });

  const reload = useCallback(async () => {
    try {
      setData(await loadRef.current());
      setError(undefined);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    const run = async () => {
      try {
        const result = await loadRef.current();
        if (!cancelled) {
          setData(result);
          setError(undefined);
        }
      } catch (e) {
        if (!cancelled) setError(e instanceof Error ? e.message : String(e));
      }
    };
    void run();
    const timer = pollMs ? setInterval(run, pollMs) : undefined;
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, [key, pollMs]);

  return { data, error, reload };
}
