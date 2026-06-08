import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

const EMPTY_ERRORS = { email: '', password: '' };

const IconClock = () => (
  <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="9" />
    <polyline points="12 7 12 12 15.5 14" />
  </svg>
);

const IconMail = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-faint)', pointerEvents: 'none' }}>
    <rect x="2" y="4" width="20" height="16" rx="2" />
    <polyline points="2,4 12,13 22,4" />
  </svg>
);

const IconLock = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ position: 'absolute', left: '0.75rem', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-faint)', pointerEvents: 'none' }}>
    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
  </svg>
);

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState(EMPTY_ERRORS);
  const [serverError, setServerError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { user, loading, login } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!loading && user) navigate('/dashboard', { replace: true });
  }, [user, loading, navigate]);

  const validate = () => {
    const errors = { email: '', password: '' };
    let hasError = false;
    if (!email.trim()) { errors.email = 'Email is required.'; hasError = true; }
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) { errors.email = 'Enter a valid email address.'; hasError = true; }
    if (!password) { errors.password = 'Password is required.'; hasError = true; }
    else if (password.length < 8) { errors.password = 'Password must be at least 8 characters.'; hasError = true; }
    return hasError ? errors : null;
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setServerError('');
    const errors = validate();
    if (errors) { setFieldErrors(errors); return; }
    setFieldErrors(EMPTY_ERRORS);
    setSubmitting(true);
    try {
      await login(email.trim(), password);
      navigate('/dashboard', { replace: true });
    } catch (err) {
      setServerError(err.message || 'Login failed. Check your credentials and try again.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div className="loading-screen">
        <div className="spinner" />
      </div>
    );
  }

  return (
    <div className="login-container">
      <div className="login-bg" />

      <div className="login-card">
        <div className="login-logo">
          <IconClock />
        </div>

        <div className="login-header">
          <h1>Welcome back</h1>
          <p>Sign in to your attendance account</p>
        </div>

        <form onSubmit={handleSubmit} noValidate>
          <div className="form-group">
            <label htmlFor="email">Email Address</label>
            <div style={{ position: 'relative' }}>
              <IconMail />
              <input
                id="email"
                type="email"
                value={email}
                onChange={(e) => { setEmail(e.target.value); setFieldErrors((fe) => ({ ...fe, email: '' })); }}
                placeholder="you@company.com"
                autoComplete="email"
                disabled={submitting}
                autoFocus
                className={fieldErrors.email ? 'input-error' : ''}
                aria-describedby={fieldErrors.email ? 'email-error' : undefined}
                style={{ paddingLeft: '2.5rem' }}
              />
            </div>
            {fieldErrors.email && <span id="email-error" className="field-error">{fieldErrors.email}</span>}
          </div>

          <div className="form-group">
            <label htmlFor="password">Password</label>
            <div style={{ position: 'relative' }}>
              <IconLock />
              <input
                id="password"
                type="password"
                value={password}
                onChange={(e) => { setPassword(e.target.value); setFieldErrors((fe) => ({ ...fe, password: '' })); }}
                placeholder="••••••••"
                autoComplete="current-password"
                disabled={submitting}
                className={fieldErrors.password ? 'input-error' : ''}
                aria-describedby={fieldErrors.password ? 'password-error' : undefined}
                style={{ paddingLeft: '2.5rem' }}
              />
            </div>
            {fieldErrors.password && <span id="password-error" className="field-error">{fieldErrors.password}</span>}
          </div>

          {serverError && <div className="error-banner">{serverError}</div>}

          <button
            type="submit"
            className="btn btn-primary btn-full"
            disabled={submitting}
            style={{ marginTop: '0.5rem' }}
          >
            {submitting ? 'Signing in…' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}
