import { useCallback } from 'react';
import { api } from '../api.js';
import { HOLDS_REFRESH_MS } from '../config.js';
import { usePolling } from '../hooks/usePolling.js';
import HoldCard from './HoldCard.jsx';

/**
 * Every application of the current student, with live status. Rule 11: because the student id is
 * kept in localStorage and this list is loaded from the server, a refresh or a reopened browser
 * shows the running applications again.
 */
export default function MyApplications({ studentId, highlightHoldId, onError, onNotice }) {
  const fetcher = useCallback(() => api.myHolds(studentId), [studentId]);
  const { data: holds, error, loading, refresh } = usePolling(fetcher, HOLDS_REFRESH_MS, {
    enabled: Boolean(studentId),
    deps: [studentId],
  });

  if (!studentId) {
    return (
      <section className="card">
        <h2>My applications</h2>
        <p className="info">Enter the student’s 17-digit birth registration number above to see their applications.</p>
      </section>
    );
  }

  return (
    <section className="card">
      <div className="card-header">
        <h2>My applications</h2>
        <button type="button" className="secondary" onClick={refresh}>
          Refresh
        </button>
      </div>

      {error && (
        <p className="field-error" role="alert">
          {holds ? 'Live update failed, showing the last known state. ' : ''}
          {error.message}
        </p>
      )}

      {loading && !holds && <p className="muted">Loading…</p>}

      {holds && holds.length === 0 && <p className="muted">No applications yet. Pick a class in the “Classes” tab and press Apply.</p>}

      {holds && holds.length > 0 && (
        <div className="hold-list">
          {holds.map((hold) => (
            <HoldCard
              key={hold.holdId}
              hold={hold}
              studentId={studentId}
              highlighted={hold.holdId === highlightHoldId}
              onChanged={refresh}
              onError={onError}
              onNotice={onNotice}
            />
          ))}
        </div>
      )}
    </section>
  );
}
