import { useState, useEffect, useCallback, useRef } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { clockApi } from '../api/clock';
import { NavBar } from '../components/NavBar';

const DEBOUNCE_MS = 2000;
const MONTHS = ['January','February','March','April','May','June','July','August','September','October','November','December'];

const timeFormatter = new Intl.DateTimeFormat('de-CH', {
  timeZone: 'Europe/Zurich',
  hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
});

const dateFormatter = new Intl.DateTimeFormat('en-GB', {
  timeZone: 'Europe/Zurich',
  weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
});

function formatTimestamp(ts) {
  if (!ts) return '—';
  return new Date(ts).toLocaleString('de-CH', { timeZone: 'Europe/Zurich', dateStyle: 'medium', timeStyle: 'short' });
}

function formatElapsed(ms) {
  if (ms == null || ms < 0) return null;
  const totalSecs = Math.floor(ms / 1000);
  const h = Math.floor(totalSecs / 3600);
  const m = Math.floor((totalSecs % 3600) / 60);
  const s = totalSecs % 60;
  return `${h}h ${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`;
}

function formatTime(ts) {
  if (!ts) return '—';
  return new Date(ts).toLocaleTimeString('de-CH', { timeZone: 'Europe/Zurich', hour: '2-digit', minute: '2-digit' });
}

function formatDate(isoDate) {
  const [y, m, d] = isoDate.split('-');
  return `${d}.${m}.${y}`;
}

function formatDuration(clockIn, clockOut) {
  if (!clockIn || !clockOut) return '—';
  const mins = Math.round((new Date(clockOut) - new Date(clockIn)) / 60000);
  if (mins < 0) return '—';
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return `${h}h ${String(m).padStart(2, '0')}m`;
}

function toZurichDate(ts) {
  return new Date(ts).toLocaleDateString('en-CA', { timeZone: 'Europe/Zurich' });
}

function buildDailyRows(events) {
  const byDate = {};
  const sorted = [...events].sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));
  for (const ev of sorted) {
    const date = toZurichDate(ev.timestamp);
    if (!byDate[date]) byDate[date] = { date, sessions: [], _openIn: null };
    const row = byDate[date];
    if (ev.eventType === 'ClockIn') {
      row._openIn = ev.timestamp;
    } else if (ev.eventType === 'ClockOut') {
      row.sessions.push({ clockIn: row._openIn, clockOut: ev.timestamp });
      row._openIn = null;
    }
  }
  for (const row of Object.values(byDate)) {
    if (row._openIn) { row.sessions.push({ clockIn: row._openIn, clockOut: null }); }
    delete row._openIn;
  }
  return Object.values(byDate).sort((a, b) => b.date.localeCompare(a.date));
}

function getYearOptions() {
  const current = new Date().getFullYear();
  const years = [];
  for (let y = current; y >= 2024; y--) years.push(y);
  return years;
}

const IconIn = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
    <polyline points="10 17 15 12 10 7" />
    <line x1="15" y1="12" x2="3" y2="12" />
  </svg>
);

const IconOut = () => (
  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4" />
    <polyline points="16 17 21 12 16 7" />
    <line x1="21" y1="12" x2="9" y2="12" />
  </svg>
);

export function DashboardPage() {
  const { user } = useAuth();
  const [activeTab, setActiveTab] = useState('clock');
  const [now, setNow] = useState(() => new Date());

  const [status, setStatus] = useState(null);
  const [loadingStatus, setLoadingStatus] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const lastClickRef = useRef(0);

  const nowDate = new Date();
  const [historyMonth, setHistoryMonth] = useState(nowDate.getMonth() + 1);
  const [historyYear, setHistoryYear] = useState(nowDate.getFullYear());
  const [historyRows, setHistoryRows] = useState([]);
  const [historyLoading, setHistoryLoading] = useState(false);
  const [historyError, setHistoryError] = useState('');

  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);
  }, []);

  const fetchStatus = useCallback(async () => {
    try {
      const data = await clockApi.status();
      setStatus(data);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoadingStatus(false);
    }
  }, []);

  const fetchHistory = useCallback(async () => {
    setHistoryLoading(true);
    setHistoryError('');
    try {
      const events = await clockApi.history(historyMonth, historyYear);
      setHistoryRows(buildDailyRows(events ?? []));
    } catch (err) {
      setHistoryError(err.message);
    } finally {
      setHistoryLoading(false);
    }
  }, [historyMonth, historyYear]);

  useEffect(() => { fetchStatus(); }, [fetchStatus]);
  useEffect(() => { if (activeTab === 'history') fetchHistory(); }, [activeTab, fetchHistory]);

  const handleClock = async (action) => {
    const ts = Date.now();
    if (ts - lastClickRef.current < DEBOUNCE_MS) return;
    lastClickRef.current = ts;
    setError('');
    setSuccessMsg('');
    setActionLoading(true);
    try {
      if (action === 'in') { await clockApi.clockIn(); setSuccessMsg('Clocked in successfully.'); }
      else                 { await clockApi.clockOut(); setSuccessMsg('Clocked out successfully.'); }
      await fetchStatus();
    } catch (err) {
      setError(err.message);
    } finally {
      setActionLoading(false);
    }
  };

  const isClockedIn = status?.isClockedIn ?? false;
  const lastEvent = status?.lastEvent;
  const lastClockInDate = isClockedIn && lastEvent?.eventType === 'ClockIn' ? new Date(lastEvent.timestamp) : null;
  const elapsedStr = formatElapsed(lastClockInDate ? now - lastClockInDate : null);

  return (
    <div className="app-layout">
      <NavBar />
      <div className="main-wrapper">
        <main className="main-content">
          <div className="page-header">
            <h1>Welcome back, {user?.firstName ?? user?.fullName?.split(' ')[0]}</h1>
            <p>Track your attendance and view your work history</p>
          </div>

          <div className="clock-card">
            <div className="clock-time">{timeFormatter.format(now)}</div>
            <div className="clock-date">{dateFormatter.format(now)}</div>
            <div className="clock-tz">Europe / Zurich</div>
          </div>

          <div style={{ height: '1.25rem' }} />

          <div className="tabs">
            <button className={`tab-btn${activeTab === 'clock' ? ' tab-active' : ''}`} onClick={() => setActiveTab('clock')}>
              Clock
            </button>
            <button className={`tab-btn${activeTab === 'history' ? ' tab-active' : ''}`} onClick={() => setActiveTab('history')}>
              My History
            </button>
          </div>

          {activeTab === 'clock' && (
            loadingStatus ? (
              <div className="loading-inline">Loading status…</div>
            ) : (
              <div className="bento-grid">
                <div className="card">
                  <div className="card-title">Current Status</div>

                  <div className={`status-indicator ${isClockedIn ? 'status-in' : 'status-out'}`}>
                    <span className="status-dot" />
                    {isClockedIn ? 'Clocked In' : 'Clocked Out'}
                  </div>

                  {lastEvent && (
                    <div className="last-event">
                      Last event:&nbsp;
                      <strong>{lastEvent.eventType === 'ClockIn' ? 'Clock In' : 'Clock Out'}</strong>
                      &nbsp;at {formatTimestamp(lastEvent.timestamp)}
                    </div>
                  )}

                  {!lastEvent && (
                    <p className="text-muted" style={{ marginTop: '0.5rem', fontSize: '0.875rem' }}>No clock events yet.</p>
                  )}

                  {lastClockInDate && (
                    <div className="session-info">
                      <div className="session-row">
                        <span className="session-label">Clocked in at</span>
                        <span className="session-value">{formatTimestamp(lastClockInDate)}</span>
                      </div>
                      <div className="session-row">
                        <span className="session-label">Time elapsed</span>
                        <span className="session-value session-elapsed">{elapsedStr}</span>
                      </div>
                    </div>
                  )}
                </div>

                <div className="card">
                  <div className="card-title">Clock Action</div>

                  {error      && <div className="error-banner">{error}</div>}
                  {successMsg && <div className="success-banner">{successMsg}</div>}

                  <div className="action-btn-group">
                    <button
                      className="action-btn action-btn-in"
                      onClick={() => handleClock('in')}
                      disabled={actionLoading || isClockedIn}
                    >
                      <IconIn />
                      {actionLoading ? 'Processing…' : 'Clock In'}
                    </button>
                    <button
                      className="action-btn action-btn-out"
                      onClick={() => handleClock('out')}
                      disabled={actionLoading || !isClockedIn}
                    >
                      <IconOut />
                      {actionLoading ? 'Processing…' : 'Clock Out'}
                    </button>
                  </div>
                </div>
              </div>
            )
          )}

          {activeTab === 'history' && (
            <div>
              <div className="section-header">
                <h2>My Attendance History</h2>
                <div className="filter-row" style={{ margin: 0 }}>
                  <select
                    value={historyMonth}
                    onChange={(e) => setHistoryMonth(Number(e.target.value))}
                    className="filter-select"
                  >
                    {MONTHS.map((m, i) => <option key={i + 1} value={i + 1}>{m}</option>)}
                  </select>
                  <select
                    value={historyYear}
                    onChange={(e) => setHistoryYear(Number(e.target.value))}
                    className="filter-select"
                  >
                    {getYearOptions().map((y) => <option key={y} value={y}>{y}</option>)}
                  </select>
                </div>
              </div>

              {historyError && <div className="error-banner">{historyError}</div>}

              {historyLoading ? (
                <div className="loading-inline">Loading history…</div>
              ) : (
                <div className="table-wrapper">
                  <table className="data-table">
                    <thead>
                      <tr>
                        <th>Date</th>
                        <th>Clock In</th>
                        <th>Clock Out</th>
                        <th>Hours Worked</th>
                      </tr>
                    </thead>
                    <tbody>
                      {historyRows.length === 0 && (
                        <tr><td colSpan={4} className="table-empty">No clock events for this month.</td></tr>
                      )}
                      {historyRows.flatMap((row) =>
                        row.sessions.map((s, i) => (
                          <tr key={`${row.date}-${i}`}>
                            <td>{i === 0 ? formatDate(row.date) : ''}</td>
                            <td>{formatTime(s.clockIn)}</td>
                            <td>{formatTime(s.clockOut)}</td>
                            <td className="td-hours">{formatDuration(s.clockIn, s.clockOut)}</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
