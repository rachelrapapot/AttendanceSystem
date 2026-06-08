# Attendance System

A full-stack employee attendance tracking application built with **React** (frontend) and **ASP.NET Core** (backend), backed by **Azure SQL Server**.

## Overview

Employees clock in and out via a web interface. Admins can manage employees, view reports, and inspect audit logs. All timestamps are sourced from an external time API (Europe/Zurich timezone) — the system never trusts browser or server time.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Frontend | React 18, Vite, React Router v6 |
| Backend | ASP.NET Core 8 Web API |
| Database | SQL Server (Azure SQL) |
| Auth | JWT (HS256) + opaque refresh tokens in httpOnly cookies |
| ORM | Entity Framework Core 8 |

## Project Structure

```
AttendanceSystem/
├── AttendanceSystem.API/          # ASP.NET Core Web API
│   ├── Controllers/               # AuthController, ClockController, AdminController
│   ├── Services/                  # Auth, Attendance, Admin, Time, Token, Audit, AppMetrics
│   ├── Models/                    # Employee, Role, RefreshToken, ClockEvent, AuditLog
│   ├── DTOs/                      # Request/response shapes
│   ├── Data/                      # AppDbContext + EF migrations
│   ├── Middleware/                # Exception, SecurityHeaders, RequestLogging
│   └── Filters/                   # PasswordComplexityAttribute
├── AttendanceSystem.Tests/        # xUnit integration tests (34 passing)
└── attendance-frontend/           # Vite + React app
    └── src/
        ├── api/                   # fetch clients: auth, clock, admin
        ├── contexts/              # AuthContext (JWT-free, cookie-based)
        ├── components/            # ProtectedRoute, AdminRoute, NavBar
        └── pages/                 # Login, Dashboard, Admin
```

## Roles

| Role | Permissions |
|------|-------------|
| **Employee** | Clock in/out, view own status and history |
| **Admin** | All employee permissions + manage employees, view reports & audit logs, correct clock events |

## API Endpoints

### Public
- `POST /api/auth/login`
- `POST /api/auth/refresh`

### Authenticated (any role)
- `POST /api/auth/logout`
- `GET  /api/auth/me`
- `POST /api/clock/in`
- `POST /api/clock/out`
- `GET  /api/clock/status`
- `GET  /api/clock/history`

### Admin only
- `GET/POST /api/admin/employees`
- `PUT/DELETE /api/admin/employees/{id}`
- `GET /api/admin/reports`
- `GET /api/admin/audit-logs`
- `PUT /api/admin/clock-events/{id}`

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- SQL Server instance (local or Azure SQL)

### 1. Configure the backend

Copy `AttendanceSystem.API/appsettings.json` and fill in your values:

```json
{
  "ConnectionStrings": {
    "Default": "Server=...;Database=AttendanceDb;..."
  },
  "Jwt": {
    "Secret": "<at-least-32-character-secret>",
    "Issuer": "AttendanceSystem",
    "Audience": "AttendanceSystem"
  },
  "Cors": {
    "AllowedOrigin": "http://localhost:3000"
  }
}
```

> The app will refuse to start if any of these keys are missing or if `Jwt:Secret` is shorter than 32 characters.

### 2. Apply database migrations

```bash
cd AttendanceSystem.API
dotnet ef database update
```

### 3. Run the backend

```bash
cd AttendanceSystem.API
dotnet run
# Listens on http://localhost:5000
```

### 4. Run the frontend

```bash
cd attendance-frontend
npm install
npm run dev
# Opens at http://localhost:3000
```

The Vite dev server proxies all `/api/*` requests to `http://localhost:5000`.

## Security

- **Authentication**: JWT access tokens (15 min) + opaque refresh tokens (7 days), both stored in `httpOnly; Secure; SameSite=Strict` cookies. Tokens are never exposed to JavaScript.
- **Refresh token rotation**: A new token is issued on every refresh. If a revoked token is replayed, all tokens for that employee are immediately revoked.
- **Passwords**: BCrypt with cost factor 12. Complexity rules enforced: uppercase, lowercase, digit, not in common-password list.
- **Rate limiting**: Max 5 login attempts per IP per 15 minutes.
- **Brute-force detection**: 5+ failures for the same email or 10+ failures from the same IP in 15 minutes triggers a `LogCritical` alert.
- **Security headers**: `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Content-Security-Policy`, `Referrer-Policy`, `Permissions-Policy` set on every response.
- **Audit log**: Every state-changing action is written to `AuditLogs` inside the same database transaction as the change. The table is append-only.
- **Soft deletes**: Employees are never hard-deleted (`IsActive = false`).

## Timestamps

All clock event timestamps come from `https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich`.  
If the API is unreachable, clock-in/out returns **503 Service Unavailable** — the system never falls back to server or browser time.

The frontend displays all times in the `Europe/Zurich` locale.

## Observability

The backend exposes metrics via `System.Diagnostics.Metrics` (meter name: `AttendanceSystem`):

```bash
dotnet-counters monitor --name AttendanceSystem.API --counters AttendanceSystem
```

Tracked counters: `time-api-latency-ms`, `time-api-failures`, `login-failures`, `clock-in-attempts`, `clock-in-successes`, `clock-out-attempts`, `clock-out-successes`.

A background service (`TimeApiHealthMonitor`) probes the time API every 60 seconds and logs `Critical` after 5 consecutive minutes of downtime.

## Running Tests

```bash
cd AttendanceSystem.Tests
dotnet test
```

34 integration tests cover authentication flows, clock event logic, admin operations, and edge cases (concurrent events, inactive accounts, stolen token detection).

## Production Checklist

- [ ] Replace HS256 with RS256 and store the private key in Azure Key Vault / environment secrets
- [ ] Set `Secure=true` on cookies (requires HTTPS)
- [ ] Wire `LogCritical` to an alerting sink (Application Insights, PagerDuty, email)
- [ ] Add an OpenTelemetry exporter for the metrics (Prometheus, Application Insights)
- [ ] Create the first Admin user via a seed script or direct DB insert
- [ ] Set `CORS:AllowedOrigin` to your production domain
