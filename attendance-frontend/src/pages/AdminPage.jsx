import { useState, useEffect, useCallback } from 'react';
import { adminApi } from '../api/admin';
import { NavBar } from '../components/NavBar';

function formatTimestamp(ts) {
  if (!ts) return '—';
  return new Date(ts).toLocaleString('de-CH', { timeZone: 'Europe/Zurich', dateStyle: 'medium', timeStyle: 'short' });
}

function formatMinutes(minutes) {
  if (minutes == null) return '—';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return `${h}h ${String(m).padStart(2, '0')}m`;
}

const INITIAL_FORM = { firstName: '', lastName: '', email: '', password: '', role: 'Employee' };
const EMPTY_FORM_ERRORS = { firstName: '', lastName: '', email: '', password: '', role: '' };
const SERVER_FIELD_MAP = { FirstName: 'firstName', LastName: 'lastName', Email: 'email', Password: 'password', Role: 'role' };

function validatePassword(pwd) {
  const issues = [];
  if (pwd.length < 8) issues.push('at least 8 characters');
  if (!/[A-Z]/.test(pwd)) issues.push('an uppercase letter');
  if (!/[a-z]/.test(pwd)) issues.push('a lowercase letter');
  if (!/[0-9]/.test(pwd)) issues.push('a number');
  if (issues.length === 0) return '';
  return `Password must contain ${issues.join(', ')}.`;
}

function mapServerErrors(fieldErrors) {
  const result = {};
  for (const [key, msgs] of Object.entries(fieldErrors || {})) {
    const field = SERVER_FIELD_MAP[key] ?? key.charAt(0).toLowerCase() + key.slice(1);
    result[field] = Array.isArray(msgs) ? msgs[0] : msgs;
  }
  return result;
}

const MONTH_NAMES = ['January','February','March','April','May','June','July','August','September','October','November','December'];

function exportCsv(reports, month, year) {
  const escape = (v) => {
    const s = String(v ?? '');
    return /[,"\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
  };
  const toTime = (ts) => ts
    ? new Date(ts).toLocaleTimeString('de-CH', { timeZone: 'Europe/Zurich', hour: '2-digit', minute: '2-digit' })
    : '';
  const toHours = (mins) => (mins != null ? (mins / 60).toFixed(2) : '');

  const rows = [['Employee Name', 'Email', 'Date', 'Clock In', 'Clock Out', 'Hours Worked']];
  for (const r of reports) {
    for (const d of r.dailyBreakdown ?? []) {
      for (const s of d.sessions ?? []) {
        rows.push([
          escape(r.fullName),
          escape(r.email),
          escape(d.date),
          escape(toTime(s.clockIn)),
          escape(toTime(s.clockOut)),
          escape(toHours(s.isComplete ? s.minutesWorked : null)),
        ]);
      }
    }
  }

  const csv = '﻿' + rows.map((r) => r.join(',')).join('\r\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `hours-report-${MONTH_NAMES[month - 1]}-${year}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

const IconChevronDown = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="6 9 12 15 18 9" />
  </svg>
);

const IconChevronUp = () => (
  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="18 15 12 9 6 15" />
  </svg>
);

export function AdminPage() {
  const now = new Date();
  const [activeTab, setActiveTab] = useState('employees');
  const [employees, setEmployees] = useState([]);
  const [reports, setReports] = useState([]);
  const [auditLogs, setAuditLogs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [showAddForm, setShowAddForm] = useState(false);
  const [form, setForm] = useState(INITIAL_FORM);
  const [formFieldErrors, setFormFieldErrors] = useState(EMPTY_FORM_ERRORS);
  const [formServerError, setFormServerError] = useState('');
  const [formSubmitting, setFormSubmitting] = useState(false);
  const [reportMonth, setReportMonth] = useState(now.getMonth() + 1);
  const [reportYear, setReportYear] = useState(now.getFullYear());
  const [includeInactive, setIncludeInactive] = useState(false);
  const [expandedEmployee, setExpandedEmployee] = useState(null);
  const [empSearch, setEmpSearch] = useState('');
  const [empStatusFilter, setEmpStatusFilter] = useState('');

  const loadData = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [emps, reps, logs] = await Promise.all([
        adminApi.getEmployees(),
        adminApi.getReports(reportMonth, reportYear, includeInactive),
        adminApi.getAuditLogs(),
      ]);
      setEmployees(emps ?? []);
      setReports(reps ?? []);
      setAuditLogs(logs ?? []);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }, [reportMonth, reportYear, includeInactive]);

  useEffect(() => { loadData(); }, [loadData]);

  const handleAddEmployee = async (e) => {
    e.preventDefault();
    setFormServerError('');
    const errors = { ...EMPTY_FORM_ERRORS };
    let hasError = false;
    if (!form.firstName.trim()) { errors.firstName = 'First name is required.'; hasError = true; }
    if (!form.lastName.trim())  { errors.lastName  = 'Last name is required.';  hasError = true; }
    if (!form.email.trim()) {
      errors.email = 'Email is required.'; hasError = true;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email.trim())) {
      errors.email = 'Enter a valid email address.'; hasError = true;
    }
    const pwdError = validatePassword(form.password);
    if (pwdError) { errors.password = pwdError; hasError = true; }
    if (hasError) { setFormFieldErrors(errors); return; }
    setFormFieldErrors(EMPTY_FORM_ERRORS);
    setFormSubmitting(true);
    try {
      await adminApi.createEmployee({
        firstName: form.firstName.trim(),
        lastName:  form.lastName.trim(),
        email:     form.email.trim(),
        password:  form.password,
        role:      form.role,
      });
      setForm(INITIAL_FORM);
      setFormFieldErrors(EMPTY_FORM_ERRORS);
      setShowAddForm(false);
      await loadData();
    } catch (err) {
      if (err.fieldErrors) {
        setFormFieldErrors((prev) => ({ ...prev, ...mapServerErrors(err.fieldErrors) }));
      } else {
        setFormServerError(err.message || 'Failed to create employee.');
      }
    } finally {
      setFormSubmitting(false);
    }
  };

  const handleDeactivate = async (id, fullName) => {
    if (!window.confirm(`Deactivate ${fullName}? They will no longer be able to log in.`)) return;
    try {
      await adminApi.deactivateEmployee(id);
      await loadData();
    } catch (err) {
      setError(err.message);
    }
  };

  const updateForm = (field) => (e) => {
    setForm((f) => ({ ...f, [field]: e.target.value }));
    setFormFieldErrors((fe) => ({ ...fe, [field]: '' }));
  };

  const q = empSearch.trim().toLowerCase();
  const filteredEmployees = employees.filter((emp) => {
    const matchesSearch = !q || emp.fullName?.toLowerCase().includes(q) || emp.email?.toLowerCase().includes(q);
    const matchesStatus = !empStatusFilter || emp.status === empStatusFilter;
    return matchesSearch && matchesStatus;
  });

  return (
    <div className="app-layout">
      <NavBar />
      <div className="main-wrapper">
        <main className="main-content">
          <div className="page-header">
            <h1>Admin Panel</h1>
            <p>Manage employees, reports, and audit activity</p>
          </div>

          <div className="tabs">
            <button className={`tab-btn${activeTab === 'employees' ? ' tab-active' : ''}`} onClick={() => setActiveTab('employees')}>Employees</button>
            <button className={`tab-btn${activeTab === 'reports'   ? ' tab-active' : ''}`} onClick={() => setActiveTab('reports')}>Hours Report</button>
            <button className={`tab-btn${activeTab === 'audit'     ? ' tab-active' : ''}`} onClick={() => setActiveTab('audit')}>Audit Log</button>
          </div>

          {error && <div className="error-banner">{error}</div>}

          {loading ? (
            <div className="loading-inline">Loading data…</div>
          ) : activeTab === 'employees' ? (
            <div>
              <div className="section-header">
                <h2>
                  {filteredEmployees.length === employees.length
                    ? `${employees.length} Employees`
                    : `${filteredEmployees.length} of ${employees.length} Employees`}
                </h2>
                <button
                  className="btn btn-primary btn-sm"
                  onClick={() => { setShowAddForm((v) => !v); setFormServerError(''); setFormFieldErrors(EMPTY_FORM_ERRORS); setForm(INITIAL_FORM); }}
                >
                  {showAddForm ? 'Cancel' : '+ Add Employee'}
                </button>
              </div>

              <div className="filter-row">
                <input
                  type="search"
                  placeholder="Search by name or email…"
                  value={empSearch}
                  onChange={(e) => setEmpSearch(e.target.value)}
                />
                <select
                  className="filter-select"
                  value={empStatusFilter}
                  onChange={(e) => setEmpStatusFilter(e.target.value)}
                >
                  <option value="">All statuses</option>
                  <option value="Active">Active</option>
                  <option value="Inactive">Inactive</option>
                  <option value="Terminated">Terminated</option>
                </select>
              </div>

              {showAddForm && (
                <div className="card add-form-card">
                  <h3>New Employee</h3>
                  <form onSubmit={handleAddEmployee} noValidate>
                    <div className="form-row-4">
                      <div className="form-group">
                        <label>First Name</label>
                        <input value={form.firstName} onChange={updateForm('firstName')} placeholder="Jane" disabled={formSubmitting} className={formFieldErrors.firstName ? 'input-error' : ''} />
                        {formFieldErrors.firstName && <span className="field-error">{formFieldErrors.firstName}</span>}
                      </div>
                      <div className="form-group">
                        <label>Last Name</label>
                        <input value={form.lastName} onChange={updateForm('lastName')} placeholder="Smith" disabled={formSubmitting} className={formFieldErrors.lastName ? 'input-error' : ''} />
                        {formFieldErrors.lastName && <span className="field-error">{formFieldErrors.lastName}</span>}
                      </div>
                      <div className="form-group">
                        <label>Email</label>
                        <input type="email" value={form.email} onChange={updateForm('email')} placeholder="jane@company.com" disabled={formSubmitting} className={formFieldErrors.email ? 'input-error' : ''} />
                        {formFieldErrors.email && <span className="field-error">{formFieldErrors.email}</span>}
                      </div>
                      <div className="form-group">
                        <label>Password</label>
                        <input type="password" value={form.password} onChange={updateForm('password')} placeholder="Min 8 chars, A-Z, a-z, 0-9" disabled={formSubmitting} className={formFieldErrors.password ? 'input-error' : ''} />
                        {formFieldErrors.password && <span className="field-error">{formFieldErrors.password}</span>}
                      </div>
                      <div className="form-group">
                        <label>Role</label>
                        <select value={form.role} onChange={updateForm('role')} disabled={formSubmitting}>
                          <option value="Employee">Employee</option>
                          <option value="Admin">Admin</option>
                        </select>
                      </div>
                    </div>
                    {formServerError && <div className="error-banner">{formServerError}</div>}
                    <button type="submit" className="btn btn-primary" disabled={formSubmitting}>
                      {formSubmitting ? 'Creating…' : 'Create Employee'}
                    </button>
                  </form>
                </div>
              )}

              <div className="table-wrapper">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Email</th>
                      <th>Role</th>
                      <th>Status</th>
                      <th>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredEmployees.length === 0 && (
                      <tr><td colSpan={5} className="table-empty">{employees.length === 0 ? 'No employees found.' : 'No employees match your filters.'}</td></tr>
                    )}
                    {filteredEmployees.map((emp) => (
                      <tr key={emp.employeeId} className={emp.status !== 'Active' ? 'row-inactive' : ''}>
                        <td className="td-name">{emp.fullName}</td>
                        <td style={{ color: 'var(--text-muted)' }}>{emp.email}</td>
                        <td><span className={`role-badge role-${emp.role?.toLowerCase()}`}>{emp.role}</span></td>
                        <td>
                          <span className={emp.status === 'Active' ? 'status-pill status-active' : 'status-pill status-inactive'}>
                            {emp.status}
                          </span>
                        </td>
                        <td>
                          {emp.status === 'Active' && (
                            <button className="btn btn-danger btn-sm" onClick={() => handleDeactivate(emp.employeeId, emp.fullName)}>
                              Deactivate
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>

          ) : activeTab === 'reports' ? (
            <div>
              <div className="section-header">
                <h2>Hours Report</h2>
                <div className="filter-row" style={{ margin: 0 }}>
                  <select value={reportMonth} onChange={(e) => setReportMonth(Number(e.target.value))} className="filter-select">
                    {MONTH_NAMES.map((m, i) => <option key={i+1} value={i+1}>{m}</option>)}
                  </select>
                  <select value={reportYear} onChange={(e) => setReportYear(Number(e.target.value))} className="filter-select">
                    {[2024, 2025, 2026, 2027].map(y => <option key={y} value={y}>{y}</option>)}
                  </select>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.875rem', cursor: 'pointer', userSelect: 'none', color: 'var(--text-muted)', whiteSpace: 'nowrap' }}>
                    <input type="checkbox" checked={includeInactive} onChange={(e) => setIncludeInactive(e.target.checked)} style={{ width: 'auto' }} />
                    Show inactive
                  </label>
                  <button
                    className="btn btn-secondary btn-sm"
                    disabled={reports.length === 0}
                    onClick={() => exportCsv(reports, reportMonth, reportYear)}
                  >
                    Export CSV
                  </button>
                </div>
              </div>

              <div className="table-wrapper">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Employee</th>
                      <th>Email</th>
                      <th>Total Hours</th>
                      <th>Days</th>
                      <th style={{ width: '32px' }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {reports.length === 0 && (
                      <tr><td colSpan={5} className="table-empty">No data for this period.</td></tr>
                    )}
                    {reports.map((r) => {
                      const completeDays   = r.dailyBreakdown?.filter(d =>  d.isComplete) ?? [];
                      const incompleteDays = r.dailyBreakdown?.filter(d => !d.isComplete) ?? [];
                      const hasRows = r.dailyBreakdown?.length > 0;
                      const daysSummary = (() => {
                        const c = completeDays.length, i = incompleteDays.length;
                        if (i === 0) return `${c} day${c !== 1 ? 's' : ''}`;
                        return `${c} complete, ${i} incomplete`;
                      })();
                      return (
                        <>
                          <tr
                            key={r.employeeId}
                            style={{ cursor: hasRows ? 'pointer' : 'default', background: expandedEmployee === r.employeeId ? 'var(--primary-dim)' : '' }}
                            onClick={() => setExpandedEmployee(expandedEmployee === r.employeeId ? null : r.employeeId)}
                          >
                            <td className="td-name">{r.fullName}</td>
                            <td style={{ color: 'var(--text-muted)' }}>{r.email}</td>
                            <td className="td-hours"><strong>{formatMinutes(r.totalMinutesWorked)}</strong></td>
                            <td style={{ color: 'var(--text-muted)', fontSize: '0.825rem' }}>{daysSummary}</td>
                            <td style={{ color: 'var(--text-faint)', textAlign: 'center' }}>
                              {hasRows ? (expandedEmployee === r.employeeId ? <IconChevronUp /> : <IconChevronDown />) : null}
                            </td>
                          </tr>
                          {expandedEmployee === r.employeeId && r.dailyBreakdown?.flatMap((d) =>
                            d.sessions?.map((s, i) => (
                              <tr key={`${d.date}-${i}`} style={{ background: s.isComplete ? 'rgba(52,211,153,0.04)' : 'rgba(251,191,36,0.04)', fontSize: '0.85em' }}>
                                <td style={{ paddingLeft: '2rem', color: s.isComplete ? 'var(--text-muted)' : 'var(--warning)' }}>
                                  {i === 0 ? d.date : ''}{!s.isComplete && ' — missing clock-out'}
                                </td>
                                <td style={{ color: 'var(--text-muted)' }}>
                                  In:&nbsp;{new Date(s.clockIn).toLocaleTimeString('de-CH', { timeZone: 'Europe/Zurich', hour: '2-digit', minute: '2-digit' })}
                                  &nbsp;→&nbsp;
                                  Out:&nbsp;{s.clockOut ? new Date(s.clockOut).toLocaleTimeString('de-CH', { timeZone: 'Europe/Zurich', hour: '2-digit', minute: '2-digit' }) : 'missing'}
                                </td>
                                <td className="td-hours">{s.isComplete ? formatMinutes(s.minutesWorked) : '—'}</td>
                                <td></td><td></td>
                              </tr>
                            )) ?? []
                          )}
                        </>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            </div>

          ) : (
            <div>
              <div className="section-header">
                <h2>Audit Log</h2>
              </div>
              <div className="table-wrapper">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th>Time</th>
                      <th>Employee ID</th>
                      <th>Action</th>
                      <th>Details</th>
                    </tr>
                  </thead>
                  <tbody>
                    {auditLogs.length === 0 && (
                      <tr><td colSpan={4} className="table-empty">No audit logs.</td></tr>
                    )}
                    {auditLogs.map((log) => (
                      <tr key={log.auditId}>
                        <td style={{ color: 'var(--text-muted)', fontSize: '0.825rem', fontVariantNumeric: 'tabular-nums' }}>{formatTimestamp(log.timestamp)}</td>
                        <td style={{ color: 'var(--text-muted)' }}>{log.employeeId ?? '—'}</td>
                        <td><span className="role-badge">{log.action}</span></td>
                        <td><code style={{ fontSize: '0.8rem', color: 'var(--text-muted)', fontFamily: 'monospace' }}>{log.details}</code></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
