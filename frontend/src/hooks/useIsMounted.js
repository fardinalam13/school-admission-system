import { useEffect, useRef } from 'react';

/**
 * Returns a ref whose `current` is true while the component is mounted. Used to drop the result of
 * a request that finishes after the component has gone away, for example a confirm that completes
 * after the student id was changed, so it cannot show a message meant for the previous student.
 */
export function useIsMounted() {
  const mounted = useRef(false);

  useEffect(() => {
    // Set on every mount (React StrictMode mounts, unmounts and remounts in development).
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  return mounted;
}
