import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * Calls `fetcher` immediately and then every `intervalMs`, keeping the last good data when a
 * refresh fails so the page never goes blank. Set `enabled` to false to pause (e.g. no student id).
 *
 * Returns { data, error, loading, refresh } where `refresh` triggers an immediate re-fetch.
 */
export function usePolling(fetcher, intervalMs, { enabled = true, deps = [] } = {}) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(enabled);
  const fetcherRef = useRef(fetcher);
  fetcherRef.current = fetcher;

  const refresh = useCallback(async () => {
    try {
      const result = await fetcherRef.current();
      setData(result);
      setError(null);
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      return undefined;
    }
    let cancelled = false;
    setLoading(true);

    const run = () => {
      if (!cancelled && document.visibilityState !== 'hidden') refresh();
    };
    run();
    const id = setInterval(run, intervalMs);
    document.addEventListener('visibilitychange', run);

    return () => {
      cancelled = true;
      clearInterval(id);
      document.removeEventListener('visibilitychange', run);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, intervalMs, refresh, ...deps]);

  return { data, error, loading, refresh };
}
