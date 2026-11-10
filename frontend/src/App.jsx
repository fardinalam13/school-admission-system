import { useCallback, useEffect, useState } from 'react';
import { STUDENT_ID_STORAGE_KEY } from './config.js';
import { isValidStudentId } from './utils/studentId.js';
import StudentIdBar from './components/StudentIdBar.jsx';
import ErrorBanner from './components/ErrorBanner.jsx';
import ClassList from './components/ClassList.jsx';
import MyApplications from './components/MyApplications.jsx';
import CreateClassForm from './components/CreateClassForm.jsx';

/** Success messages disappear on their own after this long; errors stay until dismissed. */
export const NOTICE_AUTO_HIDE_MS = 5000;

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

  // A notice is an object rather than a string so that the same text reported twice still restarts
  // the auto-hide timer below.
  const reportNotice = useCallback((text) => {
    setError(null);
    setNotice({ text, id: Date.now() + Math.random() });
  }, []);

  // Success messages are transient: hide them automatically after a few seconds.
  useEffect(() => {
    if (!notice) return undefined;
    const timer = setTimeout(() => setNotice(null), NOTICE_AUTO_HIDE_MS);
    return () => clearTimeout(timer);
  }, [notice]);

  const clearMessages = useCallback(() => {
    setError(null);
    setNotice(null);
  }, []);

  // Messages belong to the screen they were raised on, so switching tabs clears them.
  const selectTab = useCallback(
    (nextTab) => {
      if (nextTab === tab) return;
      setTab(nextTab);
      clearMessages();
    },
    [tab, clearMessages],
  );

  // Messages and the highlighted application belong to the student who caused them. When a
  // different id is entered, nothing from the previous student may remain on screen.
  const changeStudentId = useCallback(
    (nextId) => {
      if (nextId === studentId) return;
      setStudentId(nextId);
      clearMessages();
      setHighlightHoldId(null);
    },
    [studentId, clearMessages],
  );

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
        <StudentIdBar value={studentId} onChange={changeStudentId} />
      </header>

      <nav className="tabs" aria-label="Sections">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            className={tab === t.id ? 'tab active' : 'tab'}
            onClick={() => selectTab(t.id)}
            aria-current={tab === t.id ? 'page' : undefined}
          >
            {t.label}
          </button>
        ))}
      </nav>

      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {notice && (
        <div className="notice" role="status">
          {notice.text}
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
            // A new key per student gives every student a fresh component: the previous student's
            // list is discarded immediately and any of their requests still in flight cannot
            // update the screen when they finish.
            key={studentIdValid ? studentId : 'no-student'}
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
