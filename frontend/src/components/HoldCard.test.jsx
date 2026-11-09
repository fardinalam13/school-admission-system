import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import HoldCard from './HoldCard.jsx';

vi.mock('../api.js', () => ({
  api: { confirmHold: vi.fn(), cancelHold: vi.fn() },
}));

function renderCard(hold) {
  return render(
    <HoldCard
      hold={hold}
      studentId="20152691234567890"
      highlighted={false}
      onChanged={() => {}}
      onError={() => {}}
      onNotice={() => {}}
    />,
  );
}

const baseHold = {
  holdId: 'h_1',
  classId: 'c_1',
  className: 'Class 6 - Morning Shift',
  status: 'HELD',
};

describe('HoldCard (rule 9: expiry without reload)', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-11-01T04:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows the countdown and an enabled confirm button while the hold is active', () => {
    renderCard({ ...baseHold, expiresAt: '2026-11-01T04:10:00Z' });

    expect(screen.getByTestId('status')).toHaveTextContent('Seat on hold');
    expect(screen.getByTestId('countdown')).toHaveTextContent('10:00');
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeEnabled();
  });

  it('switches to EXPIRED and disables confirm when the deadline passes, with no reload', () => {
    renderCard({ ...baseHold, expiresAt: '2026-11-01T04:00:05Z' });
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeEnabled();

    // Let 6 seconds of wall-clock time pass; the component ticks every second.
    act(() => {
      vi.advanceTimersByTime(6000);
    });

    expect(screen.getByTestId('status')).toHaveTextContent('Expired');
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
    expect(screen.queryByTestId('countdown')).not.toBeInTheDocument();
  });

  it('renders a hold whose expiry is already in the past as EXPIRED immediately', () => {
    renderCard({ ...baseHold, expiresAt: '2026-11-01T03:59:00Z' });

    expect(screen.getByTestId('status')).toHaveTextContent('Expired');
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
  });

  it('keeps a CONFIRMED admission confirmed even after its original deadline', () => {
    renderCard({ ...baseHold, status: 'CONFIRMED', expiresAt: '2026-11-01T03:00:00Z', confirmedAt: '2026-11-01T02:59:00Z' });

    expect(screen.getByTestId('status')).toHaveTextContent('Admission confirmed');
    expect(screen.getByRole('button', { name: 'Pay admission fee' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeEnabled();
  });
});
