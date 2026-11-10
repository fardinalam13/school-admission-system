import '@testing-library/jest-dom/vitest';
import { configure } from '@testing-library/react';

// findBy*/waitFor give up after 1 s by default. On a slow or busy machine the first render of the
// app can take longer than that, which made a test fail intermittently. Allow more headroom.
configure({ asyncUtilTimeout: 5000 });

// Recent Node versions (25+) define their own experimental `localStorage` global, which is
// undefined unless Node is started with --localstorage-file. It hides the browser-like storage the
// jsdom test environment provides, so tests that touch localStorage would crash depending on which
// Node version runs them. Provide a minimal in-memory Storage whenever the global one is unusable.
if (typeof globalThis.localStorage?.getItem !== 'function') {
  const store = new Map();
  const memoryStorage = {
    get length() {
      return store.size;
    },
    key: (index) => Array.from(store.keys())[index] ?? null,
    getItem: (key) => (store.has(String(key)) ? store.get(String(key)) : null),
    setItem: (key, value) => {
      store.set(String(key), String(value));
    },
    removeItem: (key) => {
      store.delete(String(key));
    },
    clear: () => {
      store.clear();
    },
  };
  Object.defineProperty(globalThis, 'localStorage', { value: memoryStorage, configurable: true, writable: true });
}
