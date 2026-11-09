import { getApiBaseUrl } from './config.js';

/**
 * Error thrown for any failed request. `message` is always something a parent can read.
 * `code` is the backend's stable error code (or a client-side one) for callers that want to
 * react differently, e.g. NO_SEATS_AVAILABLE vs HOLD_EXPIRED.
 */
export class ApiError extends Error {
  constructor(message, { status = 0, code = 'UNKNOWN' } = {}) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

/** Human wording for backend error codes. Falls back to the backend's own `detail` text. */
const FRIENDLY_MESSAGES = {
  NO_SEATS_AVAILABLE: 'Sorry, this class is full right now. A seat may free up if another application expires, so you can try again later.',
  DUPLICATE_APPLICATION: 'You already have an application or a confirmed admission in this class.',
  HOLD_EXPIRED: 'The time limit for this application has passed. Please apply again if seats are available.',
  HOLD_CANCELLED: 'This application was cancelled and can no longer be confirmed.',
  NOT_HOLD_OWNER: 'This application belongs to a different student id.',
  CLASS_NOT_FOUND: 'That class no longer exists. The list will refresh.',
  HOLD_NOT_FOUND: 'That application could not be found. It may have been removed.',
  STUDENT_ID_MISSING: 'Please enter the student’s 17-digit birth registration number at the top of the page first.',
  STUDENT_ID_INVALID: 'The student id must be exactly 17 digits.',
  VALIDATION_FAILED: null, // use the backend's specific detail text
};

async function request(method, path, { studentId, body } = {}) {
  const headers = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';
  if (studentId) headers['X-Student-Id'] = studentId;

  let response;
  try {
    response = await fetch(`${getApiBaseUrl()}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    // fetch only rejects on network-level failure: server down, DNS, CORS, offline.
    throw new ApiError('Cannot reach the admission server. Please check your connection and try again in a moment.', {
      code: 'NETWORK_ERROR',
    });
  }

  if (response.status === 204) return null;

  let data = null;
  const text = await response.text();
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = null;
    }
  }

  if (!response.ok) {
    const code = data?.code ?? `HTTP_${response.status}`;
    const friendly = FRIENDLY_MESSAGES[code];
    const message =
      friendly ??
      data?.detail ??
      data?.title ??
      (response.status >= 500
        ? 'The server had a problem handling that request. Please try again.'
        : `The request failed (HTTP ${response.status}).`);
    throw new ApiError(message, { status: response.status, code });
  }

  return data;
}

// --- The eight endpoints from the brief ---------------------------------------------------------

export const api = {
  createClass: (name, totalSeats) => request('POST', '/classes', { body: { name, totalSeats } }),
  listClasses: () => request('GET', '/classes'),
  getClass: (classId) => request('GET', `/classes/${encodeURIComponent(classId)}`),
  createHold: (classId, studentId) => request('POST', `/classes/${encodeURIComponent(classId)}/holds`, { studentId }),
  getHold: (holdId) => request('GET', `/holds/${encodeURIComponent(holdId)}`),
  confirmHold: (holdId, studentId) => request('POST', `/holds/${encodeURIComponent(holdId)}/confirm`, { studentId }),
  cancelHold: (holdId, studentId) => request('DELETE', `/holds/${encodeURIComponent(holdId)}`, { studentId }),
  myHolds: (studentId) => request('GET', '/me/holds', { studentId }),
};
