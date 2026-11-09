/**
 * Rule 12: every error (no seats, expired, bad input, server down) must be shown to the user in
 * plain language. This is the single place errors surface; nothing is left only in the console.
 */
export default function ErrorBanner({ message, onDismiss }) {
  if (!message) return null;
  return (
    <div className="error-banner" role="alert">
      <span>{message}</span>
      {onDismiss && (
        <button type="button" className="link" onClick={onDismiss} aria-label="Dismiss error">
          ×
        </button>
      )}
    </div>
  );
}
