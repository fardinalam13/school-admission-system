import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, ApiError } from './api.js';

function mockFetch(status, body) {
  globalThis.fetch = vi.fn().mockResolvedValue({
    ok: status >= 200 && status < 300,
    status,
    text: async () => (body === undefined ? '' : JSON.stringify(body)),
  });
}

describe('api client (rule 12: errors in human language)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('sends the student id in the X-Student-Id header', async () => {
    mockFetch(201, { holdId: 'h_1', status: 'HELD' });

    await api.createHold('c_1', '20152691234567890');

    const [url, options] = globalThis.fetch.mock.calls[0];
    expect(url).toBe('http://localhost:8080/classes/c_1/holds');
    expect(options.method).toBe('POST');
    expect(options.headers['X-Student-Id']).toBe('20152691234567890');
  });

  it('turns a 409 NO_SEATS_AVAILABLE into a friendly message with the code preserved', async () => {
    mockFetch(409, { status: 409, detail: 'No seats are available.', code: 'NO_SEATS_AVAILABLE' });

    const error = await api.createHold('c_1', '20152691234567890').catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error.status).toBe(409);
    expect(error.code).toBe('NO_SEATS_AVAILABLE');
    expect(error.message).toMatch(/full/i);
  });

  it('uses the backend detail text for validation failures', async () => {
    mockFetch(400, { status: 400, detail: 'totalSeats must be a whole number between 1 and 100000.', code: 'VALIDATION_FAILED' });

    const error = await api.createClass('X', 0).catch((e) => e);

    expect(error.message).toBe('totalSeats must be a whole number between 1 and 100000.');
  });

  it('explains a network failure instead of throwing a raw TypeError', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'));

    const error = await api.listClasses().catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error.code).toBe('NETWORK_ERROR');
    expect(error.message).toMatch(/cannot reach the admission server/i);
  });

  it('returns null for a 204 No Content cancel', async () => {
    mockFetch(204);

    await expect(api.cancelHold('h_1', '20152691234567890')).resolves.toBeNull();
  });
});
