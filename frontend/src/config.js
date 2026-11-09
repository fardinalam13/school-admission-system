/**
 * Where the backend lives. Resolution order:
 *  1. window.__APP_CONFIG__.API_BASE_URL  – written at container start from the API_BASE_URL env var
 *  2. import.meta.env.VITE_API_BASE_URL    – build-time variable for `npm run dev` / `npm run build`
 *  3. http://localhost:8080                – the port fixed by the assignment brief
 */
export function getApiBaseUrl() {
  const runtime = typeof window !== 'undefined' ? window.__APP_CONFIG__?.API_BASE_URL : undefined;
  const buildTime = import.meta.env?.VITE_API_BASE_URL;
  const url = runtime || buildTime || 'http://localhost:8080';
  return url.replace(/\/+$/, '');
}

/** How often (ms) the class list re-fetches seat counts. Rule 10: "every few seconds". */
export const SEAT_REFRESH_MS = 3000;

/** How often (ms) the "my applications" list re-fetches, to pick up server-side state changes. */
export const HOLDS_REFRESH_MS = 5000;

/** localStorage key for the student id so it survives refresh and browser restarts (rule 11). */
export const STUDENT_ID_STORAGE_KEY = 'school-admission.studentId';
