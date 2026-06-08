# מדריך מלא לפרויקט מערכת נוכחות — הכנה לראיון

---

## תוכן עניינים

1. [סקירת הפרויקט](#1-סקירת-הפרויקט)
2. [ארכיטקטורה כללית](#2-ארכיטקטורה-כללית)
3. [מסד הנתונים](#3-מסד-הנתונים)
4. [Backend — שכבת ה-API](#4-backend--שכבת-ה-api)
5. [Frontend — כל עמוד ורכיב](#5-frontend--כל-עמוד-ורכיב)
6. [זרימת האימות (Authentication Flow)](#6-זרימת-האימות)
7. [זרימת Clock In / Clock Out](#7-זרימת-clock-in--clock-out)
8. [החלטות ארכיטקטוניות מרכזיות](#8-החלטות-ארכיטקטוניות-מרכזיות)
9. [שאלות ותשובות לראיון](#9-שאלות-ותשובות-לראיון)

---

## 1. סקירת הפרויקט

### מה בנינו?
מערכת שעון נוכחות המאפשרת לעובדים להחתים כניסה ויציאה ממשמרת, עם ממשק ניהול מלא למנהל.

### מחסנית טכנולוגית (Tech Stack)

| שכבה | טכנולוגיה |
|------|-----------|
| Frontend | React 18 + Vite (port 3000) |
| Backend | ASP.NET Core 8 Web API (port 5012) |
| Database | Azure SQL Database (Microsoft SQL Server) |
| ORM | Entity Framework Core 8 (Code-First) |
| Authentication | JWT בתוך httpOnly Cookies |
| שעה חיצונית | timeapi.io — אזור זמן Europe/Zurich |
| Password Hashing | BCrypt (work factor 12) |

### עיקרון בסיסי חשוב
**כל פעולת Clock In / Clock Out מתבצעת על בסיס שעה שמגיעה מ-API חיצוני (timeapi.io) בלבד.**
אין שימוש בשעון הדפדפן ואין שימוש בשעון השרת המקומי.

---

## 2. ארכיטקטורה כללית

```
Browser (React)
    ↕  HTTP (withCredentials: cookies)
ASP.NET Core API (port 5012)
    ├── Middleware Pipeline
    │     ├── ExceptionMiddleware      ← תופס כל שגיאה וממיר ל-JSON מסודר
    │     ├── SecurityHeadersMiddleware ← headers אבטחה (CSP, HSTS, X-Frame-Options)
    │     ├── RequestLoggingMiddleware  ← לוג כל request/response
    │     ├── IpRateLimiting           ← הגבלת קצב לפי IP
    │     ├── CORS                     ← מאפשר רק http://localhost:3000
    │     ├── Authentication           ← קורא JWT מ-cookie
    │     └── Authorization            ← בודק role (Admin/Employee)
    ├── Controllers
    │     ├── AuthController    → /api/auth/*
    │     ├── ClockController   → /api/clock/*
    │     └── AdminController   → /api/admin/*
    ├── Services (Business Logic)
    │     ├── AuthService, TokenService, AdminService
    │     ├── AttendanceService, TimeService, AuditService
    │     └── AppMetrics, TimeApiHealthMonitor
    └── Data Layer (EF Core)
          └── Azure SQL Database
```

---

## 3. מסד הנתונים

### טבלאות

#### `Roles` — תפקידים
```
RoleId (PK) | RoleName (unique)
     1       |   Employee
     2       |   Admin
```
נוצר בזמן Migration (Seed Data) — לא ניתן לשנות דרך ה-API.

#### `Employees` — עובדים
```
EmployeeId (PK, auto)
Email (unique, max 256)
PasswordHash (BCrypt, max 256)
FirstName, LastName (max 100)
RoleId (FK → Roles)
Status (Active / Inactive / Terminated) — CHECK constraint
CreatedAt (datetimeoffset)
```
- `PasswordHash` — אף פעם לא מוחזר ב-API responses
- `Status` — מוגן ב-CHECK constraint ברמת ה-DB

#### `ClockEvents` — אירועי נוכחות
```
EventId (PK, auto)
EmployeeId (FK → Employees, CASCADE DELETE)
EventType (ClockIn / ClockOut) — CHECK constraint
Timestamp (datetimeoffset) — השעה מ-timeapi.io
CreatedAt (datetimeoffset)
```
- אינדקסים על `EmployeeId` ועל `Timestamp` לביצועים מהירים

#### `RefreshTokens` — טוקנים לחידוש סשן
```
Id (PK, auto)
EmployeeId (FK → Employees, CASCADE DELETE)
TokenHash (SHA-256 של הטוקן, max 256)
ExpiresAt (datetimeoffset)
RevokedAt (nullable — null = פעיל, יש ערך = בוטל)
CreatedAt (datetimeoffset)
```
- אף פעם לא שומרים את הטוקן הגולמי — רק ה-Hash שלו

#### `AuditLogs` — יומן ביקורת
```
AuditId (PK, auto)
EmployeeId (nullable FK → Employees, SET NULL on delete)
Action (ClockIn / ClockOut / Login / LogoutFailed / AdminAction) — CHECK constraint
Details (nvarchar(max), JSON)
Timestamp (datetimeoffset)
CreatedAt (datetimeoffset)
```

---

## 4. Backend — שכבת ה-API

### Controllers

#### `AuthController` — /api/auth/*

| Endpoint | Method | תיאור |
|----------|--------|-------|
| `/api/auth/login` | POST | התחברות — מחזיר 2 cookies |
| `/api/auth/logout` | POST | התנתקות — מבטל refresh token |
| `/api/auth/refresh` | POST | מחדש access token |
| `/api/auth/me` | GET | מחזיר פרופיל המשתמש המחובר |

**כיצד עובד Login:**
1. קבלת email+password
2. בדיקת BCrypt מול hash ב-DB
3. יצירת access_token (JWT, 15 דקות)
4. יצירת refresh_token (random 64 bytes → Base64)
5. שמירת **SHA-256 hash** של refresh_token ב-DB
6. הגדרת שני httpOnly cookies בתשובה

#### `ClockController` — /api/clock/*

| Endpoint | Method | תיאור |
|----------|--------|-------|
| `/api/clock/in` | POST | החתמת כניסה |
| `/api/clock/out` | POST | החתמת יציאה |
| `/api/clock/status` | GET | סטטוס נוכחי (מחובר/לא) |
| `/api/clock/history` | GET | היסטוריה עם פילטר חודש/שנה |

#### `AdminController` — /api/admin/* (Admin role בלבד)

| Endpoint | Method | תיאור |
|----------|--------|-------|
| `/api/admin/employees` | GET | כל העובדים |
| `/api/admin/employees` | POST | יצירת עובד חדש |
| `/api/admin/employees/{id}` | PUT | עדכון עובד |
| `/api/admin/employees/{id}` | DELETE | סיום העסקה (soft delete) |
| `/api/admin/reports` | GET | דוח שעות עם פירוט יומי |
| `/api/admin/audit-logs` | GET | יומן ביקורת מלא |
| `/api/admin/clock-events/{id}` | PUT | תיקון timestamp של אירוע |

### Services (Logic Layer)

#### `AuthService`
- `LoginAsync` — אימות, יצירת tokens, לוג ביקורת, זיהוי brute-force (5+ כישלונות ב-15 דקות → LogCritical)
- `RefreshAsync` — **Refresh Token Rotation** — טוקן ישן מבוטל, טוקן חדש ניתן
  - **Stolen Token Detection**: אם מנסים להשתמש בטוקן שכבר בוטל → ביטול כל הסשנים של אותו משתמש
- `LogoutAsync` — ביטול refresh token ב-DB

#### `TokenService`
- `GenerateAccessToken` — JWT עם claims: sub (employeeId), email, role, jti, firstName, lastName
- `GenerateRawRefreshToken` — 64 bytes random (cryptographically secure via `RandomNumberGenerator`)
- `HashToken` — SHA-256 hex lowercase

#### `AttendanceService`
- `ClockInAsync` — בדיקה שהעובד לא כבר מחובר היום. שימוש ב-**SERIALIZABLE transaction** למניעת race condition במקרה של בקשות מקבילות.
- `ClockOutAsync` — בדיקה שיש clock-in פתוח היום. SERIALIZABLE transaction.
- `GetReportsAsync` — בניית דוח שעות עם פירוט יומי ו-sessions. מסנן עובדים לא פעילים כברירת מחדל.

#### `TimeService`
- קורא ל-`https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich`
- **Polly Resilience Pipeline:**
  - **Retry**: 3 ניסיונות חוזרים עם Exponential Backoff (500ms, 1000ms, 2000ms)
  - **Circuit Breaker**: אם 50%+ מהבקשות נכשלות → פותח circuit ל-30 שניות

#### `AuditService`
- מתעד כל פעולה חשובה: login, logout, clockIn, clockOut, adminAction
- הפרטים נשמרים כ-JSON ב-Details

#### `AppMetrics`
- מדידת מונים: LoginFailures, ClockInAttempts, ClockInSuccesses, TimeApiFailures, TimeApiLatencyMs

#### `TimeApiHealthMonitor`
- Background service שבודק את timeapi.io כל דקה
- אם לא זמין במשך 5 דקות רצופות → `LogCritical`

### Middleware

#### `ExceptionMiddleware`
מטפל בכל החריגות ומחזיר JSON מסודר:
- `TimeApiUnavailableException` → 503 Service Unavailable
- `AccountInactiveException` → 403 Forbidden
- `DbUpdateException` → 409 Conflict (unique constraint violation)
- `InvalidOperationException` → 400 Bad Request
- `Exception` כללי → 500 (בפרודקשן: הודעה גנרית; בדיבאג: הודעה מלאה)

#### `SecurityHeadersMiddleware`
מוסיף headers:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: no-referrer`
- `Content-Security-Policy` (מרוכך ל-/swagger/*)

#### `RequestLoggingMiddleware`
לוג של כל request ו-response עם: Method, Path, Status Code, Duration

---

## 5. Frontend — כל עמוד ורכיב

### מבנה הקבצים

```
attendance-frontend/src/
├── api/
│   ├── client.js        ← wrapper לכל HTTP calls
│   ├── auth.js          ← login, logout, me
│   └── admin.js         ← employees, reports, audit-logs
├── contexts/
│   └── AuthContext.jsx  ← global auth state
├── components/
│   ├── NavBar.jsx       ← navigation bar
│   ├── ProtectedRoute.jsx ← guard: צריך להיות מחובר
│   └── AdminRoute.jsx   ← guard: צריך להיות Admin
├── pages/
│   ├── LoginPage.jsx    ← עמוד ההתחברות
│   ├── DashboardPage.jsx ← עמוד העובד
│   └── AdminPage.jsx    ← עמוד הניהול
└── App.jsx              ← routing ראשי
```

---

### `App.jsx` — הנתב הראשי

```jsx
<AuthProvider>           ← עוטף הכל, מספק user/login/logout לכל הרכיבים
  <BrowserRouter>
    /login  → LoginPage
    /dashboard → ProtectedRoute → DashboardPage
    /admin  → AdminRoute → AdminPage
    /* → Navigate to /dashboard
  </BrowserRouter>
</AuthProvider>
```

כל Route שאינו `/login` מוגן. מי שלא מחובר → redirect ל-`/login`.
מי שמחובר כ-Employee ומנסה להיכנס ל-`/admin` → redirect ל-`/dashboard`.

---

### `AuthContext.jsx` — מנהל הסשן הגלובלי

**מה הוא עושה:**
- בטעינת האפליקציה: מנסה לקבל פרופיל מ-`GET /api/auth/me`
  - אם נכשל (token פג) → מנסה `POST /api/auth/refresh` אוטומטית
  - אם גם זה נכשל → user = null (לא מחובר)
- מספק: `user`, `loading`, `login(email, password)`, `logout()`
- מאזין לאירוע `auth:sessionExpired` (נורה כשה-API מחזיר 401) → מאפס user

**נקודה חשובה:** הרענון אוטומטי (silent refresh) שקוף לחלוטין למשתמש — הסשן נמשך בלי שהוא יצטרך להתחבר מחדש כל 15 דקות.

---

### `client.js` — HTTP API Client

- wrapper סביב `fetch` עם base URL `/api`
- מגדיר `credentials: 'include'` — כדי שהדפדפן ישלח cookies אוטומטית
- מגדיר Vite proxy: בקשות ל-`/api` מועברות ל-`http://localhost:5012`
- טיפול בשגיאות: מנתח את ה-JSON של השגיאה (`{ error }` או `{ errors }` של ModelState)
- אם מגיע 401 → מנסה refresh אחד → אם נכשל → שולח אירוע `auth:sessionExpired`

---

### `LoginPage.jsx` — עמוד ההתחברות

**מה הוא עושה:**
1. טופס email + password
2. שולח `POST /api/auth/login`
3. מקבל חזרה פרופיל (firstName, lastName, role)
4. מעדכן `AuthContext.user`
5. Redirect לפי role:
   - Admin → `/admin`
   - Employee → `/dashboard`

**שגיאות:** הודעת "Invalid email or password" (לא מפרטת מה בדיוק שגוי — עיקרון אבטחה: לא לאפשר enumeration)

---

### `DashboardPage.jsx` — עמוד העובד

**מה הוא מציג:**
- שם העובד + תפקיד
- כפתור "Clock In" / "Clock Out" (מתחלפים לפי סטטוס נוכחי)
- השעה האחרונה שהוחתמה
- כפתור Logout

**Flow:**
1. בטעינה: `GET /api/clock/status` — מחזיר `{ isClockedIn: bool, lastEvent: {...} }`
2. לחיצה על Clock In → `POST /api/clock/in` → עדכון UI
3. לחיצה על Clock Out → `POST /api/clock/out` → עדכון UI
4. כל השעות מוצגות ב-timezone `Europe/Zurich`

**מקרי קצה שמטופלים:**
- כבר מחובר ומנסה Clock In שוב → שגיאה 409
- מנסה Clock Out בלי Clock In → שגיאה 409
- חשבון הושעה → שגיאה 403 עם הסבר ברור

---

### `AdminPage.jsx` — עמוד הניהול (3 טאבים)

#### טאב 1: Employees — ניהול עובדים

**מה יש בו:**
- חיפוש בזמן אמת לפי שם/אימייל (client-side filtering)
- סינון לפי סטטוס (All / Active / Inactive / Terminated)
- טבלה עם כל העובדים: שם, אימייל, תפקיד (badge), סטטוס (pill)
- כפתור "Deactivate" — מופיע רק לעובדים Active
- כפתור "+ Add Employee" — פותח טופס

**טופס יצירת עובד:**
- שדות: FirstName, LastName, Email, Password, Role
- ולידציה **דו-שכבתית:**
  1. **Client-side:** בדיקה מיידית לפני שליחה (שדות ריקים, פורמט email, דרישות סיסמה)
  2. **Server-side:** אם ה-API מחזיר 400 עם field errors → מוצגים ליד השדה הספציפי
- Mapping: `SERVER_FIELD_MAP` ממפה שמות C# (PascalCase) לשמות JS (camelCase)
- סיסמה: `validatePassword()` בודקת 4 דרישות ומחזירה הודעה מפורטת

#### טאב 2: Hours Report — דוח שעות

**מה יש בו:**
- Dropdown לבחירת חודש + שנה
- Checkbox "Show inactive" — כולל עובדים לא פעילים
- טבלה עם סיכום לכל עובד: סה"כ שעות, מספר ימים
- לחיצה על שורת עובד → **expand** — מציג פירוט יומי:
  - כל יום: שעת כניסה, שעת יציאה, סה"כ שעות
  - ימים "לא שלמים" (ללא clock-out) מסומנים בצהוב עם "missing clock-out"

**Data structure מהשרת:**
```
ReportEntry {
  employeeId, fullName, email, totalMinutesWorked, totalClockEvents,
  dailyBreakdown: DailyEntry[] {
    date, sessions: SessionEntry[], totalMinutesWorked, isComplete
  }
}
```

#### טאב 3: Audit Log — יומן ביקורת

- טבלה של כל הפעולות: זמן, Employee ID, סוג פעולה, פרטים (JSON)
- מסודר לפי זמן (החדש ביותר ראשון)

---

### `ProtectedRoute.jsx` ו-`AdminRoute.jsx`

```jsx
// ProtectedRoute — כל עובד מחובר יכול לעבור
if (!user) → redirect to /login

// AdminRoute — רק Admin יכול לעבור
if (!user) → redirect to /login
if (user.role !== 'Admin') → redirect to /dashboard
```

בזמן loading → מציג skeleton/spinner (לא מבצע redirect מוקדם)

---

## 6. זרימת האימות

```
משתמש לוחץ Login
        ↓
POST /api/auth/login
        ↓
Server: BCrypt.Verify(password, hash)
        ↓
יצירת JWT (15 min) + Refresh Token (7 days)
שמירת SHA-256(refreshToken) ב-DB
        ↓
Set-Cookie: access_token=<JWT>; HttpOnly; SameSite=Strict
Set-Cookie: refresh_token=<raw>; HttpOnly; SameSite=Strict
        ↓
AuthContext.user = { employeeId, firstName, role, ... }

--- כל בקשה ---
Browser: שולח cookies אוטומטית
Server: קורא JWT מה-cookie ומאמת
        ↓
JWT פג? → POST /api/auth/refresh
        ↓
Server: מחפש SHA-256(token) ב-DB, מאמת
Rotation: מבטל ישן, יוצר חדש
Set-Cookie: access_token (new) + refresh_token (new)
        ↓
Retry הבקשה המקורית

--- Logout ---
POST /api/auth/logout → RevokesAt = now ב-DB
Clear cookies
```

---

## 7. זרימת Clock In / Clock Out

```
עובד לוחץ Clock In
        ↓
POST /api/clock/in
        ↓
AttendanceService.ClockInAsync(employeeId, ip)
        ↓
1. GetZurichTimeAsync() → קריאה ל-timeapi.io
2. BEGIN TRANSACTION (SERIALIZABLE)
3. טעינת Employee מ-DB
4. בדיקת Status == "Active" (יכול להיות נפל אחרי הוצאת token)
5. שאילתה: האם יש ClockIn פתוח היום? (todayStart → tomorrowStart)
6. אם כן → 409 Conflict
7. שמירת ClockEvent (EventType="ClockIn", Timestamp=zurichTime)
8. AuditLog
9. COMMIT
        ↓
ClockEventResponse(eventId, employeeId, fullName, "ClockIn", timestamp)

--- Clock Out ---
1-4 זהה
5. שאילתה: האם האירוע האחרון היום הוא ClockIn?
6. אם לא → 409 Conflict
7. שמירת ClockEvent (EventType="ClockOut")
8. AuditLog + COMMIT
```

**למה SERIALIZABLE transaction?**
מונע race condition: אם שני browsers לוחצים Clock In בו-זמנית, רק אחד יצליח — השני יקבל שגיאה.

---

## 8. החלטות ארכיטקטוניות מרכזיות

### למה timeapi.io ולא שעון השרת?
הדרישה המפורשת — למנוע מניפולציה של זמן על ידי שינוי שעון השרת. timeapi.io הוא שירות עצמאי שאין לשרת גישה לשנות אותו.

### למה httpOnly Cookies ולא localStorage?
- **XSS Protection**: `httpOnly` מונע גישה של JavaScript לcookie — גם אם תוקף מצליח להזריק קוד, הוא לא יכול לגנוב את הtoken
- **SameSite=Strict**: מונע CSRF — הדפדפן לא שולח את הcookie בבקשות cross-site

### למה שני Tokens (access + refresh)?
- **Access Token (15 דקות)**: stateless — מאומת בלי DB. קצר כדי להגביל חלון ניצול במקרה של גניבה.
- **Refresh Token (7 ימים)**: מאוחסן כ-hash ב-DB — ניתן לביטול מיידי. Rotation — הישן מבוטל, חדש ניתן בכל שימוש.

### למה BCrypt עם work factor 12?
הגנה מפני brute force על hash גנוב מה-DB. כל hash לוקח ~250ms לחישוב — מספיק איטי לתוקף, מספיק מהיר למשתמש לגיטימי.

### למה SERIALIZABLE Transaction?
מניעת phantom reads בבדיקת "האם כבר מחובר היום". בלעדיה, שתי בקשות מקבילות יכולות לשתיהן לעבור את הבדיקה ולשמור שני ClockIn.

### למה Code-First Migrations?
הגדרת schema בקוד C# ויצירת DB מתוך הקוד. כל שינוי מתועד ב-Migration file ב-git.

### למה Azure SQL ולא Local SQL Server?
בגלל בעיית IO alignment בחומרת Dell Vostro 15 3530 (Hypervisor) שמנעה כל התקנה מקומית של SQL Server. Azure SQL Free Tier פתר את הבעיה — DB זמין ב-cloud ללא התקנה מקומית.

### למה Polly Retry + Circuit Breaker על timeapi.io?
timeapi.io עלול להיות לא זמין. Retry מטפל בתקלות רשת קצרות. Circuit Breaker מונע מצב שכל request תקוע 30 שניות — אחרי 3 כישלונות הcircuit נפתח ל-30 שניות ומחזיר שגיאה מיידית.

---

## 9. שאלות ותשובות לראיון

### שאלות בסיסיות על הפרויקט

**ש: הסבר/י את ארכיטקטורת הפרויקט.**
> פרויקט full-stack עם React בצד הלקוח ו-ASP.NET Core בצד השרת. ה-Frontend מתקשר עם ה-Backend דרך REST API. ה-Backend עובד עם Azure SQL Database דרך Entity Framework Core. האימות מבוסס JWT בתוך httpOnly cookies לאבטחה מפני XSS ו-CSRF.

**ש: למה בחרת ב-React + ASP.NET Core?**
> זו הדרישה — React לצד הלקוח ו-ASP.NET Core לצד השרת. השילוב הזה נפוץ מאוד בתעשייה, ASP.NET Core הוא framework מהיר ועוצמתי לבניית Web APIs, ו-React מאפשר לבנות ממשק משתמש ריאקטיבי.

**ש: למה timeapi.io ולא `DateTime.Now`?**
> הדרישה היא שכל Clock In/Out יתבסס על זמן מ-API חיצוני עבור Europe/Zurich. שימוש ב-DateTime.Now מסתמך על שעון השרת שניתן לשנות. timeapi.io הוא שירות עצמאי ואמין שמחזיר את הזמן הנוכחי ב-timezone המבוקש.

---

### שאלות אבטחה

**ש: מה יקרה אם תוקף יגנוב את ה-JWT Token?**
> ה-access token תקף רק 15 דקות — חלון ניצול קצר מאוד. ה-refresh token מאוחסן כ-SHA-256 hash בלבד — גם אם מישהו ישיג גישה ל-DB, הוא לא יכול להשתמש בhash ישירות. בנוסף, ה-token נמצא ב-httpOnly cookie — JavaScript לא יכול לקרוא אותו, אז XSS לא יעזור.

**ש: מה זה Refresh Token Rotation ולמה זה חשוב?**
> בכל שימוש ב-refresh token, הטוקן הישן מבוטל וטוקן חדש ניתן. אם תוקף גונב refresh token ומנסה להשתמש בו לאחר שהמשתמש כבר השתמש בו — השרת מזהה שמנסים לעשות replay של טוקן שכבר בוטל, ומבטל **כל** הסשנים של אותו משתמש מיד (Stolen Token Detection).

**ש: מה ההבדל בין httpOnly cookie ל-localStorage לשמירת JWT?**
> localStorage נגיש ל-JavaScript — כל קוד XSS יכול לקרוא אותו ולשלוח את הToken לשרת של התוקף. httpOnly cookie אינו נגיש ל-JavaScript בכלל — הדפדפן שולח אותו אוטומטית עם כל request, אבל שום script לא יכול לקרוא אותו.

**ש: מה זה CSRF ואיך מגנים מפניו?**
> CSRF (Cross-Site Request Forgery) הוא מצב שאתר זדוני שולח בקשה לשרת שלנו מתוך דפדפן של משתמש מחובר. מגינים עם `SameSite=Strict` — הדפדפן לא ישלח את הcookie בבקשות שמגיעות מdomain שונה.

---

### שאלות לוגיקה ו-Edge Cases

**ש: מה קורה אם עובד מנסה להחתים כניסה פעמיים?**
> השרת מחזיר 409 Conflict עם הודעה "Already clocked in. Clock out first." הבדיקה נעשית ב-SERIALIZABLE transaction — גם אם שתי בקשות מגיעות בו-זמנית, רק אחת תצליח.

**ש: מה קורה אם timeapi.io לא זמין?**
> יש Polly Resilience Pipeline: 3 ניסיונות חוזרים עם exponential backoff (500ms, 1s, 2s). אם כולם נכשלים — Circuit Breaker נפתח ל-30 שניות ומחזיר שגיאה מיידית במקום להמתין. השרת מחזיר 503 Service Unavailable עם הסבר ברור.

**ש: מה קורה אם עובד פוטר בזמן שהוא מחובר?**
> ה-JWT שלו תקף עד 15 דקות מרגע ההנפקה. אם הוא מנסה Clock In/Out לאחר הפיטורים — השרת בודק את ה-Status מה-DB (לא רק מה-token) ומחזיר 403 עם הודעה "Your account is terminated. Contact an administrator." בנוסף, ה-Refresh Token שלו בוטל ברגע הסיום.

**ש: למה SERIALIZABLE isolation level ב-Clock events?**
> Phantom Read: שתי transactions מקבילות שתיהן בודקות "האם יש ClockIn פתוח?" ושתיהן רואות "לא" — ואז שתיהן כותבות ClockIn. SERIALIZABLE מונע זאת על ידי נעילת ה-range של הנתונים שנקראו.

---

### שאלות Frontend

**ש: הסבר/י את מנגנון ה-AuthContext.**
> AuthContext הוא React Context שמנהל את מצב האימות הגלובלי. הוא מכיל את אובייקט המשתמש המחובר, פונקציות login/logout, ומצב loading. בטעינת האפליקציה הוא מנסה לשחזר סשן קיים דרך /api/auth/me — אם נכשל, מנסה refresh אוטומטי.

**ש: מה ההבדל בין ProtectedRoute ל-AdminRoute?**
> ProtectedRoute מאפשר כניסה לכל משתמש מחובר (Admin גם). AdminRoute מוסיף בדיקה שה-role הוא "Admin" — עובד רגיל שינסה לגשת ל-/admin יועבר ל-/dashboard.

**ש: איך מוצגות השעות?**
> `Intl.DateTimeFormat` עם `timeZone: 'Europe/Zurich'` ו-`locale: 'de-CH'` (שוויצרי-גרמנית). זה מתרגם את ה-ISO timestamp שמגיע מהשרת לתצוגה בזמן הנכון.

**ש: מה הבעיה שאתה פותר עם `mapServerErrors`?**
> ה-Backend מחזיר validation errors עם שמות שדות ב-PascalCase (C# style: "FirstName", "Password"). ה-Frontend מאחסן state ב-camelCase ("firstName", "password"). הפונקציה ממירה בין השניים כדי שכל שגיאה תוצג ליד השדה הנכון.

---

### שאלות Database

**ש: למה Code-First Migrations?**
> שינויים ב-schema מתועדים כקוד C# ב-git, הם reversible, ניתנים לבדיקה ב-Code Review, ומאפשרים environment אחיד (dev/staging/prod) עם אותה גרסת schema.

**ש: למה soft delete (Terminated status) ולא DELETE?**
> כדי לשמר היסטוריה. ClockEvents ו-AuditLogs של עובד שפוטר נשארים ב-DB — חשוב לדוחות שעות ולביקורת. גם אם נדרש לראות את שעות עבודתו לצורך חישוב משכורת אחרונה.

**ש: למה לא לשמור את ה-Refresh Token עצמו אלא את ה-Hash?**
> עקרון Defense in Depth: אם מישהו ישיג גישה ל-DB (SQL Injection, גיבוי דלף), הוא לא יוכל להשתמש ב-hash ישירות — הוא צריך את הטוקן הגולמי. זה בדיוק כמו שמירת password hash במקום הסיסמה עצמה.

---

### שאלות על החלטות שביצעת

**ש: מה תוסיף/י לפרויקט אם היה עוד זמן?**
> 1. Pagination לטבלאות — כרגע נטענות כל הרשומות בבת אחת. 2. CSV Export לדוחות. 3. עמוד היסטוריה אישית לעובד. 4. שינוי סיסמה עצמית. 5. בדיקות integration מקיפות יותר.

**ש: מה היה האתגר הגדול ביותר?**
> SQL Server לא הצליח להתקין על המחשב הספציפי בגלל בעיית IO alignment בחומרת Dell Vostro עם Hypervisor (שגיאה 575). פתרתי על ידי מעבר ל-Azure SQL Database Free Tier — מה שגם הוסיף ערך למוצר (DB בcloudמשמש production-ready).

**ש: למה בחרת ב-Vite ולא Create React App?**
> Vite מהיר פי 10 בזמן פיתוח — HMR (Hot Module Replacement) בפחות מ-50ms. CRA מיושן ולא מתוחזק. Vite הוא הסטנדרט החדש לפרויקטי React.

**ש: מה זה CircuitBreaker ולמה השתמשת בו?**
> Circuit Breaker הוא pattern לניהול כשלים בשירותים חיצוניים. אם timeapi.io לא עונה, במקום שכל request יחכה 5 שניות ויכשל, ה-circuit נפתח ל-30 שניות ומחזיר שגיאה מיידית — משפר חווית משתמש ומוריד עומס על השרת הכושל.

---

### שאלות "מה היית עושה אחרת"

**ש: האם הייתה לך גישה שונה ל-authentication?**
> ניתן גם לשקול Session-based auth לפשטות, אבל JWT עם httpOnly cookies נותן scalability — ה-access token מאומת stateless, ה-server לא צריך לשמור session state. לפרויקט עם עובדים רבים ועומס גבוה זה יותר יעיל.

**ש: אילו בדיקות (tests) היית כותב?**
> Integration tests שבודקים את כל ה-flow (login → clock in → clock out → report). Unit tests ל-AttendanceService (clock logic). Tests לboundary cases: ניסיון double clock-in, clock-out ללא clock-in, עובד מושעה. Tests לTimeService עם mock של timeapi.io.

---

### טיפים להצגה

1. **פתח ב-Swagger** (`http://localhost:5012/swagger`) — מרשים, מאפשר להדגים endpoints בלי Postman
2. **הדגם flow שלם**: Login → Clock In → Clock Out → דוח
3. **הסבר SERIALIZABLE transaction** — זה מרשים ומראה הבנה עמוקה
4. **הדגש את timeapi.io** — זה הדרישה המרכזית, ודא שהם רואים שהשעה מגיעה מה-API
5. **Admin panel**: צור עובד, הראה field-level validation errors, הראה דוח עם פירוט יומי
6. **הראה את ה-httpOnly cookies** ב-DevTools → Application → Cookies

---

*נוצר: 2026-06-08*
