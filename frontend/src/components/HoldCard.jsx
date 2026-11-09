import { useState } from 'react';
import { api } from '../api.js';
import { useNow } from '../hooks/useNow.js';
import { formatDateTime, formatRemaining, remainingMs } from '../utils/time.js';

const STATUS_LABEL = {
  HELD: 'Seat on hold',
  CONFIRMED: 'Admission confirmed',
  EXPIRED: 'Expired',
  CANCELLED: 'Cancelled',
};

/**
 * One application: status, time remaining, and the "Pay admission fee" / "Cancel" buttons.
 *
 * Rule 9: when the deadline passes the card switches to EXPIRED and disables the confirm button on
 * its own, driven by the local clock ticking against `expiresAt`, with no page reload and no
 * server round-trip required. The next poll then confirms the same state from the server.
 */
export default function HoldCard({ hold, studentId, highlighted, onChanged, onError, onNotice }) {
  const now = useNow(1000);
  const [busy, setBusy] = useState(null);

  const msLeft = remainingMs(hold.expiresAt, now);
  const effectiveStatus = hold.status === 'HELD' && msLeft === 0 ? 'EXPIRED' : hold.status;
  const canConfirm = effectiveStatus === 'HELD';
  const canCancel = effectiveStatus === 'HELD' || effectiveStatus === 'CONFIRMED';
  const urgent = canConfirm && msLeft < 60_000;

  async function confirm() {
    setBusy('confirm');
    try {
      // "Pay admission fee" assumes payment succeeded; no real gateway (per the brief).
      await api.confirmHold(hold.holdId, studentId);
      onNotice(`Admission confirmed for ${hold.className}.`);
    } catch (err) {
      onError(err);
    } finally {
      setBusy(null);
      onChanged();
    }
  }

  async function cancel() {
    if (!window.confirm(`Cancel the application for ${hold.className}? The seat will be released.`)) return;
    setBusy('cancel');
    try {
      await api.cancelHold(hold.holdId, studentId);
      onNotice(`Application for ${hold.className} cancelled.`);
    } catch (err) {
      onError(err);
    } finally {
      setBusy(null);
      onChanged();
    }
  }

  return (
    <article className={`hold-card status-${effectiveStatus.toLowerCase()}${highlighted ? ' highlighted' : ''}`}>
      <div className="hold-main">
        <h3>{hold.className}</h3>
        <span className={`badge badge-${effectiveStatus.toLowerCase()}`} data-testid="status">
          {STATUS_LABEL[effectiveStatus] ?? effectiveStatus}
        </span>
      </div>

      <dl className="hold-details">
        {canConfirm && (
          <>
            <dt>Time left to pay</dt>
            <dd className={urgent ? 'countdown urgent' : 'countdown'} data-testid="countdown" aria-live="polite">
              {formatRemaining(msLeft)}
            </dd>
          </>
        )}
        {effectiveStatus === 'EXPIRED' && (
          <>
            <dt>Expired at</dt>
            <dd>{formatDateTime(hold.expiresAt)}</dd>
          </>
        )}
        {effectiveStatus === 'CONFIRMED' && hold.confirmedAt && (
          <>
            <dt>Confirmed at</dt>
            <dd>{formatDateTime(hold.confirmedAt)}</dd>
          </>
        )}
        <dt>Application id</dt>
        <dd className="mono small">{hold.holdId}</dd>
      </dl>

      {effectiveStatus === 'EXPIRED' && (
        <p className="muted small">The time limit passed before the fee was paid. You can apply again if seats are available.</p>
      )}

      <div className="actions">
        <button type="button" onClick={confirm} disabled={!canConfirm || busy !== null}>
          {busy === 'confirm' ? 'Confirming…' : 'Pay admission fee'}
        </button>
        <button type="button" className="secondary" onClick={cancel} disabled={!canCancel || busy !== null}>
          {busy === 'cancel' ? 'Cancelling…' : 'Cancel'}
        </button>
      </div>
    </article>
  );
}
