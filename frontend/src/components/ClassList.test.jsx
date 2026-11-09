import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ClassList from './ClassList.jsx';
import { api, ApiError } from '../api.js';

vi.mock('../api.js', async () => {
  const actual = await vi.importActual('../api.js');
  return {
    ...actual,
    api: { listClasses: vi.fn(), createHold: vi.fn() },
  };
});

const classes = [
  { id: 'c_1', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25 },
  { id: 'c_2', name: 'Class 6 - Day Shift', totalSeats: 60, heldSeats: 10, confirmedSeats: 50, availableSeats: 0 },
];

describe('ClassList', () => {
  beforeEach(() => {
    api.listClasses.mockReset();
    api.createHold.mockReset();
    api.listClasses.mockResolvedValue(classes);
  });

  it('shows total, held, confirmed and available seats for every class', async () => {
    render(<ClassList studentId="20152691234567890" onApplied={() => {}} onError={() => {}} />);

    const row = (await screen.findByText('Class 6 - Morning Shift')).closest('tr');
    expect(row).toHaveTextContent('120');
    expect(row).toHaveTextContent('15');
    expect(row).toHaveTextContent('80');
    expect(row).toHaveTextContent('25');
  });

  it('disables Apply for a full class and without a student id', async () => {
    const { rerender } = render(<ClassList studentId="20152691234567890" onApplied={() => {}} onError={() => {}} />);

    await screen.findByText('Class 6 - Day Shift');
    const fullRow = screen.getByText('Class 6 - Day Shift').closest('tr');
    expect(fullRow.querySelector('button')).toBeDisabled();
    expect(fullRow.querySelector('button')).toHaveTextContent('Full');

    const openRow = screen.getByText('Class 6 - Morning Shift').closest('tr');
    expect(openRow.querySelector('button')).toBeEnabled();

    rerender(<ClassList studentId="" onApplied={() => {}} onError={() => {}} />);
    expect(openRow.querySelector('button')).toBeDisabled();
    expect(screen.getByText(/enter the student/i)).toBeInTheDocument();
  });

  it('reports the hold to the parent after a successful Apply', async () => {
    const hold = { holdId: 'h_1', classId: 'c_1', status: 'HELD', expiresAt: '2026-11-01T04:10:00Z' };
    api.createHold.mockResolvedValue(hold);
    const onApplied = vi.fn();

    render(<ClassList studentId="20152691234567890" onApplied={onApplied} onError={() => {}} />);
    const row = (await screen.findByText('Class 6 - Morning Shift')).closest('tr');
    await userEvent.click(row.querySelector('button'));

    await waitFor(() => expect(onApplied).toHaveBeenCalledWith(hold));
    expect(api.createHold).toHaveBeenCalledWith('c_1', '20152691234567890');
  });

  it('surfaces a readable error when the class filled up in the meantime', async () => {
    api.createHold.mockRejectedValue(new ApiError('Sorry, this class is full right now.', { status: 409, code: 'NO_SEATS_AVAILABLE' }));
    const onError = vi.fn();

    render(<ClassList studentId="20152691234567890" onApplied={() => {}} onError={onError} />);
    const row = (await screen.findByText('Class 6 - Morning Shift')).closest('tr');
    await userEvent.click(row.querySelector('button'));

    await waitFor(() => expect(onError).toHaveBeenCalled());
    expect(onError.mock.calls[0][0].message).toMatch(/full/i);
  });
});
