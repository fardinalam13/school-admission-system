import { isValidStudentId } from '../utils/studentId.js';

/**
 * The box at the top of the page where the student's 17-digit birth registration number is typed.
 * The UI uses this value as the student's identity for every request (X-Student-Id header).
 */
export default function StudentIdBar({ value, onChange }) {
  const trimmed = value.trim();
  const valid = isValidStudentId(trimmed);
  const showHint = trimmed.length > 0 && !valid;

  return (
    <div className="student-bar">
      <label htmlFor="student-id">Student birth registration number</label>
      <input
        id="student-id"
        inputMode="numeric"
        autoComplete="off"
        placeholder="17 digits, e.g. 20152691234567890"
        value={value}
        maxLength={17}
        onChange={(e) => onChange(e.target.value.replace(/\D/g, ''))}
        aria-invalid={showHint}
        aria-describedby="student-id-hint"
      />
      <span id="student-id-hint" className={valid ? 'hint ok' : 'hint'}>
        {valid
          ? 'Ready: applications will be made for this student.'
          : showHint
            ? `Must be exactly 17 digits (${trimmed.length}/17).`
            : 'Enter the number before applying.'}
      </span>
    </div>
  );
}
