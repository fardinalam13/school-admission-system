import { useEffect, useState } from 'react';

/**
 * Returns the current time and re-renders the caller every `tickMs`. Used by countdowns so the
 * remaining time (and the switch to EXPIRED) updates live without any page reload.
 */
export function useNow(tickMs = 1000) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = setInterval(() => setNow(Date.now()), tickMs);
    return () => clearInterval(id);
  }, [tickMs]);

  return now;
}
