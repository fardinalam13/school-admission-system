import { useCallback, useEffect, useState } from 'react';
import { STUDENT_ID_STORAGE_KEY } from './config.js';
import { isValidStudentId } from './utils/studentId.js';
import StudentIdBar from './components/StudentIdBar.jsx';
import ErrorBanner from './components/ErrorBanner.jsx';
import ClassList from './components/ClassList.jsx';
import MyApplications from './components/MyApplications.jsx';
import CreateClassForm from './components/CreateClassForm.jsx';

const TABS = [
  { id: 'classes', label: 'Classes' },
  { id: 'mine', label: 'My applications' },
  { id: 'admin', label: 'Admin: create class' },
];

export default function App() {
  // The student id is the only "identity" in the system (no login). It is kept in localStorage so
  // a page refresh or a closed-and-reopened browser still shows the student's applications.
  const [studentId, setStudentId] = useState(() => localStorage.getItem(STUDENT_ID_STORAGE_KEY) ?? '');
  const [tab, setTab] = useState('classes');
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [highlightHoldId, setHighlightHoldId] = useState(null);

  useEffect(() => {
    localStorage.setItem(STUDENT_ID_STORAGE_KEY, studentId);
  }, [studentId]);

  // Any component can report a failure; it is shown in one place at the top of the page.
  const reportError = useCallback((err) => {
    setNotice(null);
    setError(err?.message ?? String(err));
  }, []);

  const reportNotice = useCallback((text) => {
    setError(null);
    setNotice(text);
  }, []);

  // After a successful application, jump to "My applications" and highlight the new hold so the
  // countdown and the confirm button are immediately visible.
  const handleApplied = useCallback(
    (hold) => {
      setHighlightHoldId(hold.holdId);
      setTab('mine');
      reportNotice('Your application was submitted. Pay the admission fee before the timer runs out to confirm the seat.');
    },
    [reportNotice],
  );

  const studentIdValid = isValidStudentId(studentId);

  return (
    <div className="app">
      <header className="app-header">
        <h1>School Admission</h1>
        <StudentIdBar value={studentId} onChange={setStudentId} />
      </header>

      <nav className="tabs" aria-label="Sections">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            className={tab === t.id ? 'tab active' : 'tab'}
            onClick={() => setTab(t.id)}
            aria-current={tab === t.id ? 'page' : undefined}
          >
            {t.label}
          </button>
        ))}
      </nav>

      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {notice && (
        <div className="notice" role="status">
          {notice}
          <button type="button" className="link" onClick={() => setNotice(null)} aria-label="Dismiss message">
            ×
          </button>
        </div>
      )}

      <main>
        {tab === 'classes' && (
          <ClassList
            studentId={studentIdValid ? studentId : ''}
            onApplied={handleApplied}
            onError={reportError}
          />
        )}
        {tab === 'mine' && (
          <MyApplications
            studentId={studentIdValid ? studentId : ''}
            highlightHoldId={highlightHoldId}
            onError={reportError}
            onNotice={reportNotice}
          />
        )}
        {tab === 'admin' && <CreateClassForm onCreated={reportNotice} onError={reportError} />}
      </main>
    </div>
  );
}
