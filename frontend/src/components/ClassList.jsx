import { useRef, useState } from 'react';
import { api } from '../api.js';
import { SEAT_REFRESH_MS } from '../config.js';
import { usePolling } from '../hooks/usePolling.js';

/**
 * Class list with live seat counts. Rule 10: the numbers refresh every few seconds without a page
 * reload. The "Apply" button is only enabled when a valid student id is entered and seats remain.
 */
export default function ClassList({ studentId, onApplied, onError }) {
  const { data: classes, error, loading, refresh } = usePolling(api.listClasses, SEAT_REFRESH_MS);
  const [applyingId, setApplyingId] = useState(null);

  // Always holds the id currently entered at the top of the page, so a reply that arrives after
  // the id was changed can be recognised as belonging to the previous student.
  const currentStudentId = useRef(studentId);
  currentStudentId.current = studentId;

  async function apply(cls) {
    const requestedFor = studentId;
    setApplyingId(cls.id);
    try {
      const hold = await api.createHold(cls.id, requestedFor);
      if (currentStudentId.current === requestedFor) onApplied(hold);
    } catch (err) {
      if (currentStudentId.current === requestedFor) onError(err);
    } finally {
      setApplyingId(null);
      refresh();
    }
  }

  return (
    <section className="card">
      <div className="card-header">
        <h2>Classes</h2>
        <span className="muted small">Seat counts refresh every {SEAT_REFRESH_MS / 1000} s</span>
      </div>

      {!studentId && (
        <p className="info">Enter the student’s 17-digit birth registration number above to apply for a seat.</p>
      )}

      {error && (
        <p className="field-error" role="alert">
          {classes ? 'Live update failed, showing the last known numbers. ' : ''}
          {error.message}
        </p>
      )}

      {loading && !classes && <p className="muted">Loading classes…</p>}

      {classes && classes.length === 0 && (
        <p className="muted">No classes yet. A school admin can create one in the “Admin: create class” tab.</p>
      )}

      {classes && classes.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Class</th>
              <th className="num">Total</th>
              <th className="num">On hold</th>
              <th className="num">Confirmed</th>
              <th className="num">Available</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {classes.map((cls) => {
              const full = cls.availableSeats <= 0;
              const busy = applyingId === cls.id;
              return (
                <tr key={cls.id}>
                  <td>{cls.name}</td>
                  <td className="num">{cls.totalSeats}</td>
                  <td className="num">{cls.heldSeats}</td>
                  <td className="num">{cls.confirmedSeats}</td>
                  <td className={full ? 'num full' : 'num ok'}>{cls.availableSeats}</td>
                  <td>
                    <button
                      type="button"
                      onClick={() => apply(cls)}
                      disabled={!studentId || full || busy}
                      title={
                        !studentId
                          ? 'Enter a valid student id first'
                          : full
                            ? 'No seats available right now'
                            : 'Hold a seat in this class'
                      }
                    >
                      {busy ? 'Applying…' : full ? 'Full' : 'Apply'}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}
