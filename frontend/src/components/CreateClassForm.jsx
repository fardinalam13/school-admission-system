import { useState } from 'react';
import { api } from '../api.js';

/** Admin screen: create a class (one per shift) with its seat quota. */
export default function CreateClassForm({ onCreated, onError }) {
  const [name, setName] = useState('');
  const [totalSeats, setTotalSeats] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [fieldError, setFieldError] = useState(null);

  const seats = Number(totalSeats);
  const seatsValid = Number.isInteger(seats) && seats >= 1;
  const canSubmit = name.trim().length > 0 && seatsValid && !submitting;

  async function handleSubmit(event) {
    event.preventDefault();
    setFieldError(null);
    if (!canSubmit) return;

    setSubmitting(true);
    try {
      const created = await api.createClass(name.trim(), seats);
      onCreated(`Class "${created.name}" created with ${created.totalSeats} seats.`);
      setName('');
      setTotalSeats('');
    } catch (err) {
      // Validation problems belong next to the form; everything else goes to the global banner.
      if (err.status === 400) setFieldError(err.message);
      else onError(err);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section className="card">
      <h2>Create a class</h2>
      <p className="muted">Each shift is its own class, e.g. “Class 6 – Morning Shift” and “Class 6 – Day Shift”.</p>

      <form onSubmit={handleSubmit} className="form" noValidate>
        <label>
          Class name
          <input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="Class 6 - Morning Shift"
            maxLength={200}
            required
          />
        </label>

        <label>
          Total seats
          <input
            type="number"
            inputMode="numeric"
            min={1}
            step={1}
            value={totalSeats}
            onChange={(e) => setTotalSeats(e.target.value)}
            placeholder="120"
            required
          />
        </label>

        {totalSeats !== '' && !seatsValid && <p className="field-error">Total seats must be a whole number of at least 1.</p>}
        {fieldError && (
          <p className="field-error" role="alert">
            {fieldError}
          </p>
        )}

        <button type="submit" disabled={!canSubmit}>
          {submitting ? 'Creating…' : 'Create class'}
        </button>
      </form>
    </section>
  );
}
