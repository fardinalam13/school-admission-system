import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import App, { NOTICE_AUTO_HIDE_MS } from './App.jsx';
import { api } from './api.js';

vi.mock('./api.js', async () => {
  const actual = await vi.importActual('./api.js');
  return {
    ...actual,
    api: {
      listClasses: vi.fn(),
      createHold: vi.fn(),
      myHolds: vi.fn(),
      confirmHold: vi.fn(),
      cancelHold: vi.fn(),
      createClass: vi.fn(),
    },
  };
});

const STUDENT_A = '20152691234567890';
const STUDENT_B = '20152691234567891';
const CLASS = { id: 'c_1', name: 'Class 6 - Morning Shift', totalSeats: 2, heldSeats: 0, confirmedSeats: 0, availableSeats: 2 };

const inTenMinutes = () => new Date(Date.now() + 600_000).toISOString();

function holdFor(studentId, className = CLASS.name) {
  return {
    holdId: `h_${studentId}`,
    classId: CLASS.id,
    className,
    studentId,
    status: 'HELD',
    expiresAt: inTenMinutes(),
    createdAt: new Date().toISOString(),
    confirmedAt: null,
  };
}

function enterStudentId(value) {
  fireEvent.change(screen.getByLabelText(/student birth registration number/i), { target: { value } });
}

function openTab(name) {
  fireEvent.click(screen.getByRole('button', { name }));
}

/** Enters the id, presses Apply on the class and waits for the success message. */
async function applyAs(studentId) {
  enterStudentId(studentId);
  const row = (await screen.findByText(CLASS.name)).closest('tr');
  fireEvent.click(within(row).getByRole('button', { name: 'Apply' }));
  return screen.findByRole('status');
}

describe('App messages and student switching', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(api.listClasses).mockReset().mockResolvedValue([CLASS]);
    vi.mocked(api.myHolds).mockReset().mockImplementation(async (studentId) => [holdFor(studentId)]);
    vi.mocked(api.createHold)
      .mockReset()
      .mockImplementation(async (classId, studentId) => ({
        holdId: `h_${studentId}`,
        classId,
        studentId,
        status: 'HELD',
        expiresAt: inTenMinutes(),
      }));
  });

  // Rendered per test (after any timer setup the test needs), with an empty student id.
  function renderApp() {
    return render(<App />);
  }

  afterEach(() => {
    vi.useRealTimers();
  });

  it('clears the success message when switching to another tab', async () => {
    renderApp();
    const notice = await applyAs(STUDENT_A);
    expect(notice).toHaveTextContent(/application was submitted/i);

    openTab('Classes');

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('hides a success message on its own after a few seconds', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    renderApp();
    await applyAs(STUDENT_A);

    act(() => {
      vi.advanceTimersByTime(NOTICE_AUTO_HIDE_MS + 100);
    });

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('does not show the previous student’s message after the id is changed', async () => {
    renderApp();
    await applyAs(STUDENT_A);

    enterStudentId(STUDENT_B);

    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('removes the previous student’s applications immediately when the id is changed', async () => {
    vi.mocked(api.myHolds).mockImplementation((studentId) =>
      studentId === STUDENT_A
        ? Promise.resolve([holdFor(STUDENT_A, 'Class of student A')])
        : new Promise(() => {}), // student B's list is still loading
    );
    renderApp();

    enterStudentId(STUDENT_A);
    openTab('My applications');
    await screen.findByText('Class of student A');

    enterStudentId(STUDENT_B);

    expect(screen.queryByText('Class of student A')).not.toBeInTheDocument();
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it('ignores a reply for the previous student that arrives after the id was changed', async () => {
    let finishStudentA;
    vi.mocked(api.myHolds).mockImplementation((studentId) =>
      studentId === STUDENT_A
        ? new Promise((resolve) => {
            finishStudentA = resolve;
          })
        : Promise.resolve([]),
    );
    renderApp();

    enterStudentId(STUDENT_A);
    openTab('My applications');
    enterStudentId(STUDENT_B);
    await screen.findByText(/no applications yet/i);

    await act(async () => {
      finishStudentA([holdFor(STUDENT_A, 'Class of student A')]);
    });

    expect(screen.queryByText('Class of student A')).not.toBeInTheDocument();
    expect(screen.getByText(/no applications yet/i)).toBeInTheDocument();
  });
});
