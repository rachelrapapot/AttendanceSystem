# הסבר מפורט — כל קובץ ועמוד בפרויקט

---

# חלק א׳ — Frontend (צד הלקוח / React)

---

## 1. `main.jsx` — נקודת הכניסה של האפליקציה

**מה הוא עושה:**
זה הקובץ הראשון שרץ כשהדפדפן פותח את האפליקציה. הוא לוקח את כל האפליקציה ו"מכניס" אותה לתוך ה-HTML ב-div שנקרא `root`.

**מה כתוב בו:**
```jsx
createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>
);
```
- `createRoot` — מחבר את React לקובץ ה-HTML
- `StrictMode` — מצב פיתוח שמזהיר על שגיאות נפוצות (לא משפיע בפרודקשן)
- `App` — הרכיב הראשי של כל האפליקציה

---

## 2. `App.jsx` — הנתב הראשי

**מה הוא עושה:**
מגדיר איזה עמוד יוצג לפי ה-URL של הדפדפן. זה כמו "שולחן הניתוב" של האפליקציה.

**מה כתוב בו:**

```jsx
<AuthProvider>                           ← עוטף הכל — מספק מידע על מי מחובר
  <BrowserRouter>                        ← מאפשר ניווט בין עמודים
    <Routes>
      /login     → LoginPage             ← דף התחברות (כולם יכולים לגשת)
      /dashboard → ProtectedRoute        ← צריך להיות מחובר
                    → DashboardPage
      /admin     → AdminRoute            ← צריך להיות מחובר כ-Admin
                    → AdminPage
      /*         → redirect ל-/dashboard ← כל URL לא מוכר → למסך הראשי
    </Routes>
  </BrowserRouter>
</AuthProvider>
```

**דוגמה לזרימה:**
- משתמש נכנס ל-`/admin` בלי להיות מחובר → `AdminRoute` מעביר ל-`/login`
- משתמש רגיל (לא Admin) נכנס ל-`/admin` → `AdminRoute` מציג "Access Denied"

---

## 3. `AuthContext.jsx` — מנהל הסשן הגלובלי

**מה הוא עושה:**
זה ה"זיכרון" של האפליקציה לגבי מי מחובר. כל עמוד ורכיב יכול לשאול "מי המשתמש הנוכחי?" ולקבל תשובה מכאן.

**המשתנים שהוא שומר:**
- `user` — אובייקט עם פרטי המשתמש המחובר (שם, אימייל, תפקיד). `null` = לא מחובר
- `loading` — האם עדיין בודקים אם יש סשן קיים? (true/false)

**מה קורה בהתחלה (בטעינת האפליקציה):**
```
האפליקציה נטענת
      ↓
מנסה לקבל פרטי משתמש מ-GET /api/auth/me
      ↓
אם עובד → user = {...}, loading = false
      ↓
אם לא עובד (token פג) → מנסה POST /api/auth/refresh (חידוש אוטומטי)
      ↓
אם Refresh עובד → מנסה שוב GET /api/auth/me → user = {...}
      ↓
אם גם זה לא עובד → user = null (לא מחובר), loading = false
```

**הפונקציות שהוא מספק לכל האפליקציה:**
- `login(email, password)` — שולח request לשרת ומעדכן את user
- `logout()` — שולח request לשרת, מנקה user = null
- `user` — מי מחובר כרגע
- `loading` — האם עדיין טוען?

**מה זה event `auth:sessionExpired`:**
כאשר ה-API מחזיר 401 ו-refresh token לא עובד, `client.js` שולח אירוע מיוחד לדפדפן. `AuthContext` מאזין לזה ומנקה את user → המשתמש מועבר אוטומטית לדף ההתחברות.

---

## 4. `api/client.js` — שכבת ה-HTTP

**מה הוא עושה:**
כל בקשה שנשלחת לשרת עוברת דרך הקובץ הזה. הוא מטפל בכל הדברים ה"משעממים": headers, cookies, שגיאות, ריניו אוטומטי של tokens.

**הפונקציה המרכזית `request`:**
```
שולח בקשה HTTP לשרת (/api + הנתיב)
      ↓
תמיד שולח cookies (credentials: 'include') — הדפדפן שולח access_token אוטומטית
      ↓
קיבל 401 (לא מורשה)?
  → מנסה POST /api/auth/refresh
  → אם עבד: שולח שוב את הבקשה המקורית
  → אם לא עבד: שולח אירוע "auth:sessionExpired" ← כל האפליקציה תנוע לדף login
      ↓
קיבל 400 (ולידציה)?
  → מנסה לקרוא שגיאות לפי שדות (fieldErrors)
  → זורק Error עם פרטי השגיאה
      ↓
קיבל תגובה תקינה?
  → מחזיר JSON
```

**הפונקציות החשופות:**
```javascript
api.get('/clock/status')          ← GET request
api.post('/clock/in')             ← POST request
api.put('/admin/employees/5')     ← PUT request
api.delete('/admin/employees/5')  ← DELETE request
```

**`silentGet` ו-`silentPost`:**
פונקציות מיוחדות שלא זורקות שגיאה — אם הבקשה נכשלת, פשוט מחזירות `null`. משמשות ב-`AuthContext` לבדיקת סשן קיים בשקט.

---

## 5. `api/auth.js` — בקשות אימות

**מה הוא עושה:**
מגדיר את הבקשות הקשורות להתחברות/התנתקות.

```javascript
authApi.login(email, password)   → POST /api/auth/login
authApi.logout()                 → POST /api/auth/logout
authApi.me()                     → GET /api/auth/me
```

---

## 6. `api/clock.js` — בקשות נוכחות

**מה הוא עושה:**
מגדיר את הבקשות לשעון הנוכחות.

```javascript
clockApi.clockIn()                   → POST /api/clock/in
clockApi.clockOut()                  → POST /api/clock/out
clockApi.status()                    → GET /api/clock/status
clockApi.history(month, year)        → GET /api/clock/history?month=6&year=2026
```

---

## 7. `api/admin.js` — בקשות ניהול

**מה הוא עושה:**
מגדיר את הבקשות לניהול עובדים ודוחות.

```javascript
adminApi.getEmployees()                → GET /api/admin/employees
adminApi.createEmployee(data)          → POST /api/admin/employees
adminApi.updateEmployee(id, data)      → PUT /api/admin/employees/5
adminApi.deactivateEmployee(id)        → DELETE /api/admin/employees/5
adminApi.getReports(month, year, includeInactive) → GET /api/admin/reports?month=6&year=2026
adminApi.getAuditLogs()               → GET /api/admin/audit-logs
```

---

## 8. `ProtectedRoute.jsx` — שומר עמודים רגילים

**מה הוא עושה:**
"מגן" על עמודים שדורשים התחברות. אם מנסים לגשת ל-`/dashboard` בלי להיות מחובר — מעבירים ל-`/login`.

**הלוגיקה:**
```
עדיין טוען? → מציג spinner (לא redirect — עדיין לא יודעים אם מחובר)
       ↓
לא מחובר (user = null)? → redirect ל-/login
       ↓
מחובר? → מציג את העמוד המבוקש
```

---

## 9. `AdminRoute.jsx` — שומר עמוד הניהול

**מה הוא עושה:**
גרסה חזקה יותר של `ProtectedRoute` — לא רק צריך להיות מחובר, אלא גם להיות Admin.

**הלוגיקה:**
```
עדיין טוען? → spinner
       ↓
לא מחובר? → redirect ל-/login
       ↓
מחובר אבל לא Admin? → מציג עמוד "Access Denied" (ולא redirect — המשתמש רואה שיש לו חשבון, רק בלי הרשאה)
       ↓
מחובר כ-Admin? → מציג AdminPage
```

---

## 10. `NavBar.jsx` — סרגל הניווט (הצד שמאל)

**מה הוא עושה:**
הסרגל שמופיע בצד שמאל בכל עמוד (חוץ מ-Login). מציג לוגו, קישורי ניווט, פרטי משתמש, וכפתור יציאה.

**מה כתוב בו:**

```
Logo + שם המערכת (למעלה)
      ↓
קישור ל-Dashboard (לכולם)
קישור ל-Admin Panel (רק אם user.role === 'Admin')
      ↓
אזור תחתון:
  - אווטאר עם אותיות ראשונות של השם (getInitials)
  - שם המשתמש + תפקיד
  - כפתור Sign Out
```

**`getInitials` פונקציה:**
מחלצת את האותיות הראשונות מהשם המלא.
`"Jane Smith"` → `"JS"`, `"John"` → `"J"`

**`handleLogout`:**
קורא ל-`logout()` מ-AuthContext ואז מנווט ל-`/login`.

---

## 11. `LoginPage.jsx` — עמוד ההתחברות

**מה הוא מציג:**
עמוד מרכזי עם לוגו שעון, כותרת "Welcome back", שדה אימייל, שדה סיסמה, וכפתור "Sign In".

**מה קורה בטעינה:**
אם המשתמש כבר מחובר (`user !== null`) → מועבר ישר ל-`/dashboard` (בלי לראות את עמוד הכניסה).

**ולידציה לפני שליחה (client-side):**
```
שדה אימייל ריק? → "Email is required."
אימייל לא בפורמט נכון? → "Enter a valid email address."
שדה סיסמה ריק? → "Password is required."
סיסמה קצרה מ-8 תווים? → "Password must be at least 8 characters."
```
אם יש שגיאות → מציג אותן ליד השדה הרלוונטי ולא שולח לשרת.

**מה קורה בלחיצה על Sign In:**
```
1. ולידציה client-side
2. אם תקין → login(email, password) → POST /api/auth/login
3. אם הצליח → navigate('/dashboard')
4. אם נכשל → מציג הודעת שגיאה מהשרת ("Invalid email or password.")
5. בזמן שליחה: כפתור מציג "Signing in…" ומושבת
```

**מצבים ויזואליים:**
- כל שדה שנגע בו ויש שגיאה → border אדום + טקסט שגיאה מתחת
- כשמנקים שדה → שגיאה נעלמת מיד
- `submitting` = true → כל הטופס נעשה disabled

---

## 12. `DashboardPage.jsx` — עמוד העובד

**מה הוא מציג:**
העמוד הראשי לעובד. מורכב מ-2 טאבים: **Clock** (החתמה) ו-**My History** (היסטוריה).

### חלק עליון (תמיד מוצג):
- `שעון חי` — מתעדכן כל שנייה, מציג שעה בזמן שוויץ (Europe/Zurich)
- תאריך מלא ("Monday, 8 June 2026")
- כיתוב "Europe / Zurich"

**איך השעה מתעדכנת כל שנייה:**
```javascript
useEffect(() => {
  const id = setInterval(() => setNow(new Date()), 1000); // כל שנייה
  return () => clearInterval(id); // ניקוי כשהרכיב נסגר
}, []);
```

---

### טאב 1: Clock — החתמה

**מה מוצג:**
שני כרטיסים (cards) זה לצד זה:

**כרטיס שמאל — Current Status:**
- סטטוס: "Clocked In" (ירוק עם נקודה דולקת) / "Clocked Out" (אפור)
- אירוע אחרון: "Clock In at 08:30"
- אם מחובר: "Clocked in at 08:30 — Time elapsed: 3h 22m 15s" (מתעדכן כל שנייה)

**כרטיס ימין — Clock Action:**
- כפתור "Clock In" (ירוק) — מושבת אם כבר מחובר
- כפתור "Clock Out" (אדום) — מושבת אם לא מחובר
- הודעות הצלחה (ירוק) / שגיאה (אדום)

**`handleClock` — הלוגיקה של לחיצת כפתור:**
```
1. Debounce — אם לחצו שוב תוך 2 שניות → מתעלמים (מונע לחיצה כפולה)
2. שולח clockIn() או clockOut() לשרת
3. אם הצליח → "Clocked in successfully." + מרענן סטטוס
4. אם נכשל → מציג הודעת שגיאה
5. בזמן עיבוד: כפתורים מציגים "Processing…" ומושבתים
```

**חישוב Time Elapsed:**
```javascript
const elapsed = now - lastClockInTime;  // מיליסקונדות
// ממיר ל: "3h 22m 15s"
```
`now` מתעדכן כל שנייה → הטיימר "רץ" בזמן אמת.

---

### טאב 2: My History — היסטוריה אישית

**מה מוצג:**
טבלה עם כל ימי העבודה של העובד בחודש הנבחר:

| תאריך | Clock In | Clock Out | שעות עבודה |
|--------|----------|-----------|------------|
| 08.06.2026 | 08:30 | 17:00 | 8h 30m |
| 07.06.2026 | 09:00 | — | — |

- ימים עם clock-out חסר: Clock Out מוצג כ-"—"
- Dropdown לבחירת חודש + שנה

**`buildDailyRows` — בניית שורות הטבלה:**
מקבל רשימת אירועים (ClockIn/ClockOut) ממיין לפי תאריך ובונה "sessions" — כל ClockIn+ClockOut = session אחד.

**`formatDuration`:**
מחשב כמה זמן עבר בין ClockIn ל-ClockOut ומציג כ-"8h 30m".

---

## 13. `AdminPage.jsx` — עמוד הניהול

**מה הוא מציג:**
לוח ניהול עם 3 טאבים. רק Admin יכול לגשת לעמוד הזה.

**בטעינה:** נטען מידע מ-3 endpoints במקביל:
```javascript
Promise.all([
  getEmployees(),    // כל העובדים
  getReports(...),   // דוח שעות
  getAuditLogs(),    // יומן ביקורת
])
```

---

### טאב 1: Employees — ניהול עובדים

**חיפוש וסינון:**
```
שדה חיפוש: מסנן לפי שם או אימייל (בזמן אמת, בלי בקשה לשרת)
Dropdown סטטוס: All / Active / Inactive / Terminated
```

**טבלת עובדים:**
- שם (כחול, מודגש)
- אימייל
- תפקיד (badge — Admin = סגול, Employee = כחול)
- סטטוס (pill ירוק/אפור)
- כפתור "Deactivate" — מופיע רק לעובדים עם Status = Active

**כפתור "+ Add Employee" → פתיחת טופס:**

**טופס יצירת עובד:**
```
שדות: First Name, Last Name, Email, Password, Role (dropdown)
```

**ולידציה דו-שכבתית:**

*שכבה 1 — Client-side (לפני שליחה):*
```
First Name ריק → "First name is required."
Last Name ריק → "Last name is required."
Email ריק → "Email is required."
Email לא תקין → "Enter a valid email address."
Password: validatePassword(pwd) בודק:
  - פחות מ-8 תווים → "Password must contain at least 8 characters"
  - אין אות גדולה → "an uppercase letter"
  - אין אות קטנה → "a lowercase letter"
  - אין ספרה → "a number"
  → מחזיר הודעה אחת עם כל הבעיות: "Password must contain an uppercase letter, a number."
```

*שכבה 2 — Server-side (תגובת ה-API):*
```
אם ה-API מחזיר fieldErrors (object עם שגיאות לפי שדה)
→ mapServerErrors() ממיר PascalCase ל-camelCase
  "FirstName" → "firstName"
  "Password" → "password"
→ מציג כל שגיאה ליד השדה הנכון
```

---

### טאב 2: Hours Report — דוח שעות

**פילטרים:**
- Dropdown חודש (1-12)
- Dropdown שנה (2024-2027)
- Checkbox "Show inactive" — כדי לכלול גם עובדים לא פעילים

**טבלה ראשית — שורה לכל עובד:**
```
שם | אימייל | סה"כ שעות | ימים
```
- "3 complete, 1 incomplete" — ימים שלמים לעומת חסרי clock-out

**לחיצה על שורה → expand (פתיחת פירוט):**
```
תת-שורה לכל יום:
  תאריך + "In: 08:30 → Out: 17:00" + סה"כ שעות
  
יום ללא clock-out:
  מוצג בצהוב: "06.06.2026 — missing clock-out"
  Out: "missing"
```

**`expandedEmployee`:**
state ששומר EmployeeId של השורה הפתוחה. לחיצה שוב → סגירה.

---

### טאב 3: Audit Log — יומן ביקורת

**מה מוצג:**
טבלה של כל הפעולות שנרשמו במערכת.

| זמן | Employee ID | פעולה | פרטים |
|-----|-------------|-------|-------|
| 08.06.2026 09:00 | 3 | ClockIn | {"ip":"...", "timestamp":"..."} |
| 08.06.2026 08:55 | 1 | AdminAction | {"action":"createEmployee","email":"..."} |

- מסודר לפי זמן — החדש ביותר ראשון
- הפרטים מוצגים כ-JSON

---

# חלק ב׳ — Backend (צד השרת / ASP.NET Core)

---

## 14. `Program.cs` — הגדרת השרת

**מה הוא עושה:**
זה ה"מוח" של השרת. מגדיר את כל השירותים, middleware, ואיך השרת מאורגן.

**חלק 1 — ולידציה של הגדרות בהפעלה:**
```
ValidateRequiredConfig() רץ ראשון ובודק:
  - ConnectionStrings:Default (חיבור למסד נתונים) — לא ריק?
  - Jwt:Secret (מפתח JWT) — לא ריק ולפחות 32 תווים?
  - Seed:AdminPassword — לא ריק?
  - Jwt:Issuer, Jwt:Audience, Cors:AllowedOrigin — קיימים?

בפרודקשן: הסודות חייבים להגיע כ-Environment Variables
בדיבאג: יכולים להגיע מ-appsettings.Development.json
```

**חלק 2 — רישום שירותים (Services):**
```
AddDbContext        → חיבור לאזור SQL דרך EF Core
AddHttpClient       → HTTP client עם timeout של 5 שניות (לקריאות timeapi.io)
AddSingleton<AppMetrics> → מדדים (Counter/Histogram) — instance אחד לכל האפליקציה
AddScoped<...>      → שירותים שנוצרים מחדש לכל HTTP request
AddHostedService    → TimeApiHealthMonitor רץ ברקע
```

**חלק 3 — JWT Authentication:**
```
מגדיר שהשרת יקרא JWT מ-cookie בשם "access_token" (לא מ-Authorization header)
מגדיר ולידציה: מפתח חתימה, Issuer, Audience, תוקף
ClockSkew = Zero → לא מאריכים את ה-15 דקות בשום מרווח סליחה
```

**חלק 4 — Middleware Pipeline (הסדר חשוב!):**
```
ExceptionMiddleware        → ראשון — תופס כל שגיאה שתגיע
SecurityHeadersMiddleware  → מוסיף headers לאבטחה
RequestLoggingMiddleware   → לוג של כל request
UseIpRateLimiting          → הגבלת קצב לפי IP
UseHttpsRedirection        → הפניה ל-HTTPS
UseCors                    → מאפשר בקשות מ-http://localhost:3000
UseAuthentication          → קריאת ה-JWT cookie
UseAuthorization           → בדיקת הרשאות Role
MapControllers             → ניתוב לcontrollers
```

**לאחר הכל:**
```
SeedData.InitializeAsync() → מריץ Migrations + יוצר Admin ראשוני אם לא קיים
app.Run() → השרת מתחיל להאזין
```

---

## 15. `Data/SeedData.cs` — נתוני ברירת מחדל

**מה הוא עושה:**
רץ פעם אחת בהפעלת השרת. מוודא שמסד הנתונים מוכן ושיש Admin ראשוני.

**מה הוא עושה בדיוק:**
```
1. מריץ Migrations — מעדכן את ה-DB לגרסה האחרונה של ה-Schema
2. בודק אם טבלת Roles קיימת ויש בה נתונים
3. אם לא → מוסיף את שתי הרשומות: Employee (id=1) ו-Admin (id=2)
4. בודק אם יש כבר עובד עם תפקיד Admin
5. אם לא → יוצר Admin ראשוני מהפרמטרים ב-appsettings:
   Email: admin@company.com
   Password: Admin123! (מוחשב ל-BCrypt hash)
   Name: "System Admin"
```

**למה זה חשוב:**
בלעדיו, לא ניתן להיכנס למערכת בהתקנה חדשה — אין משתמש ראשוני.

---

## 16. `Data/AppDbContext.cs` — מנהל מסד הנתונים

**מה הוא עושה:**
זה ה-"גשר" בין הקוד C# ל-SQL Server. כל גישה לנתונים עוברת דרכו.

**הטבלאות שהוא מנהל:**
```
db.Roles         → טבלת Roles
db.Employees     → טבלת Employees
db.RefreshTokens → טבלת RefreshTokens
db.ClockEvents   → טבלת ClockEvents
db.AuditLogs     → טבלת AuditLogs
```

**`OnModelCreating` — הגדרת ה-Schema:**

*Employees:*
```
Email: unique index + max 256 תווים
Status: CHECK constraint שמאפשר רק "Active", "Inactive", "Terminated"
PasswordHash: לא נכלל ב-SELECT אוטומטי (אנחנו שולטים מה מוחזר ב-DTO)
```

*ClockEvents:*
```
EventType: CHECK constraint — רק "ClockIn" או "ClockOut"
אינדקס על Timestamp — לחיפוש מהיר לפי תאריך
```

*AuditLogs:*
```
אם עובד נמחק (Terminated) → EmployeeId ב-AuditLog הופך NULL (לא נמחק הרשומה)
```

---

## 17. `DTOs/AuthDtos.cs` — מבני נתונים לאימות

**מה הם:**
DTOs = Data Transfer Objects. הגדרה של מה נשלח לשרת ומה חוזר ממנו.

```csharp
LoginRequest:    { Email, Password }              ← מה שמגיע מהלקוח
LoginResponse:   { EmployeeId, FirstName, ... }   ← מה שחוזר אחרי login
MeResponse:      { EmployeeId, FirstName, ... }   ← מה שחוזר מ-/api/auth/me
```

**ולידציה ב-LoginRequest:**
- `[Required]` — חובה
- `[EmailAddress]` — פורמט אימייל תקין
- `[MaxLength(256)]` — לא יותר מ-256 תווים

---

## 18. `DTOs/ClockDtos.cs` — מבני נתונים לנוכחות

```csharp
ClockEventResponse:    { EventId, EmployeeId, EmployeeName, EventType, Timestamp }
ClockStatusResponse:   { IsClockedIn, LastEvent? }
ClockStatusLastEvent:  { EventId, EventType, Timestamp }
```

---

## 19. `DTOs/AdminDtos.cs` — מבני נתונים לניהול

**`CreateEmployeeRequest`:**
```csharp
{ Email, Password, FirstName, LastName, Role }
// ולידציות:
[PasswordComplexity] → בדיקת מורכבות סיסמה
[AllowedValues("Employee", "Admin")] → Role חייב להיות אחד משניהם
```

**`EmployeeResponse`:**
```csharp
{ EmployeeId, Email, FirstName, LastName, Role, Status, CreatedAt }
// FullName — computed property: FirstName + " " + LastName
```

**`SessionEntry`:**
```csharp
{ ClockIn, ClockOut?, MinutesWorked, IsComplete }
// session אחת = צמד ClockIn+ClockOut
// IsComplete = false → ClockIn ללא ClockOut תואם
```

**`DailyEntry`:**
```csharp
{ Date, Sessions[], TotalMinutesWorked, IsComplete }
// ריכוז של כל ה-sessions ביום אחד
```

**`ReportEntry`:**
```csharp
{ EmployeeId, FullName, Email, TotalMinutesWorked, TotalClockEvents, DailyBreakdown[] }
// ריכוז של כל הנתונים לעובד בתקופה המבוקשת
```

---

## 20. `Filters/PasswordComplexityAttribute.cs` — בדיקת סיסמה

**מה הוא עושה:**
Attribute מותאם אישית (custom validation attribute) שמוסיפים על שדות סיסמה.

**מה הוא בודק:**
```
יש אות גדולה (A-Z)?      — אם לא: "Password must contain at least one uppercase letter."
יש אות קטנה (a-z)?      — אם לא: "Password must contain at least one lowercase letter."
יש ספרה (0-9)?           — אם לא: "Password must contain at least one number."
הסיסמה ברשימת נפוצות?   — אם כן: "Password is too common."
  (דוגמאות: "password123", "123456", "qwerty", "Admin123!")
```

**איפה הוא בשימוש:**
ב-`CreateEmployeeRequest` ו-`UpdateEmployeeRequest` — כך שכל יצירה/עדכון של סיסמה עוברת את הבדיקות האלה.

---

## 21. `Controllers/AuthController.cs` — נתיבי אימות

**מה הוא עושה:**
מטפל בכל מה שקשור להתחברות ולניהול הסשן.

### `POST /api/auth/login`
```
מקבל: { email, password }
      ↓
קורא ל-AuthService.LoginAsync()
      ↓
אם הצליח:
  SetCookie("access_token", jwt, expires בעוד 15 דקות)
  SetCookie("refresh_token", rawToken, expires בעוד 7 ימים)
  מחזיר 200 + פרטי עובד
      ↓
אם נכשל:
  מחזיר 401 + { error: "Invalid email or password." }
```

**`SetCookie` — הגדרת cookies:**
```
HttpOnly = true     → JavaScript לא יכול לקרוא
Secure = true       → רק HTTPS (בפרודקשן)
SameSite = Strict   → לא נשלח בבקשות cross-site (הגנת CSRF)
```

### `POST /api/auth/logout`
```
קורא ל-AuthService.LogoutAsync() → מבטל refresh token ב-DB
מנקה את שני ה-cookies (מגדיר expires ל-אתמול)
מחזיר 204
```

### `POST /api/auth/refresh`
```
קורא ל-AuthService.RefreshAsync() עם ה-refresh_token cookie
      ↓
אם הצליח → cookies חדשים (access + refresh)
אם נכשל → מוחק cookies + מחזיר 401
```

### `GET /api/auth/me`
```
מחלץ EmployeeId מה-JWT claims
קורא ל-AuthService.GetEmployeeByIdAsync()
מחזיר פרטי עובד
```

---

## 22. `Controllers/ClockController.cs` — נתיבי נוכחות

**מה הוא עושה:**
מטפל בהחתמת כניסה/יציאה של עובדים.

### `POST /api/clock/in`
```
מחלץ EmployeeId מה-JWT (המשתמש המחובר)
מחלץ IP מה-Request
קורא ל-AttendanceService.ClockInAsync()
מחזיר 200 + פרטי האירוע שנשמר
```

### `POST /api/clock/out`
```
זהה ל-ClockIn, קורא ל-AttendanceService.ClockOutAsync()
```

### `GET /api/clock/status`
```
מחזיר: { isClockedIn: true/false, lastEvent: {...} }
```

### `GET /api/clock/history?month=6&year=2026`
```
מחזיר רשימת כל אירועי ה-Clock של העובד בתקופה
```

---

## 23. `Controllers/AdminController.cs` — נתיבי ניהול

**מה הוא עושה:**
כל הפעולות הניהוליות. כל endpoint מוגן ב-`[Authorize(Roles = "Admin")]`.

### `GET /api/admin/employees`
```
מחזיר רשימת כל העובדים (כולל Inactive/Terminated)
```

### `POST /api/admin/employees`
```
מאמת ModelState (ולידציה של DTO)
קורא ל-AdminService.CreateEmployeeAsync()
מחזיר 201 Created + פרטי העובד החדש
```

### `PUT /api/admin/employees/{id}`
```
מאפשר עדכון חלקי — כל שדה אופציונלי
מחזיר 200 + עובד מעודכן, או 404 אם לא נמצא
```

### `DELETE /api/admin/employees/{id}`
```
Soft Delete — מגדיר Status = "Terminated", מבטל Refresh Tokens
מחזיר 204 No Content
```

### `GET /api/admin/reports?month=6&year=2026&includeInactive=false`
```
מחזיר דוח שעות מפורט עם DailyBreakdown לכל עובד
```

### `PUT /api/admin/clock-events/{id}`
```
תיקון timestamp של אירוע Clock (למצבים שהעובד שכח להחתים)
```

**`GetAdminId()`:**
```csharp
מחלץ את EmployeeId של המנהל המחובר מה-JWT claims
שימוש: לתיעוד ב-AuditLog — "מי ביצע את הפעולה"
```

---

## 24. `Services/AuthService.cs` — לוגיקת האימות

**מה הוא עושה:**
כל לוגיקת ההתחברות, ניהול הסשן, וגילוי מתקפות.

### `LoginAsync`
```
1. GetZurichTimeAsync() — מקבל זמן נוכחי מ-timeapi.io
2. מחפש עובד לפי Email + Status == "Active"
3. BCrypt.Verify(password, hash) — בדיקת סיסמה
4. אם נכשל:
   - מוסיף AuditLog עם Action="LogoutFailed"
   - AlertOnBruteForceAsync() — בדיקת brute force
   - מחזיר null
5. אם הצליח:
   - יוצר Raw Refresh Token (64 bytes אקראיים)
   - שומר SHA-256(refreshToken) ב-DB
   - מוסיף AuditLog עם Action="Login"
   - מחזיר (employee, accessToken, rawRefreshToken)
```

### `AlertOnBruteForceAsync`
```
בודק ב-AuditLogs: כמה כישלונות לאימייל זה ב-15 דקות האחרונות?
≥ 5 כישלונות → LogCritical("[SECURITY ALERT] possible brute-force")

בודק: כמה כישלונות מה-IP הזה ב-15 דקות?
≥ 10 כישלונות → LogCritical("[SECURITY ALERT] possible credential stuffing")
```

### `RefreshAsync`
```
1. חישוב SHA-256(rawRefreshToken)
2. חיפוש ב-DB לפי ה-hash
3. אם Token כבר בוטל (RevokedAt != null):
   → זה אומר שמישהו ניסה להשתמש בטוקן גנוב!
   → ביטול כל ה-RefreshTokens של אותו עובד (Stolen Token Detection)
   → LogCritical
   → מחזיר null
4. אם פג תוקף → מחזיר null
5. אם חשבון לא פעיל → מחזיר null
6. Rotation:
   - מגדיר RevokedAt על הטוקן הישן
   - יוצר טוקן חדש ושומר hash שלו
   - מחזיר (newAccessToken, newRawRefreshToken)
```

---

## 25. `Services/TokenService.cs` — יצירת Tokens

**מה הוא עושה:**
יוצר ומאמת JWT tokens ו-Refresh tokens.

### `GenerateAccessToken`
```
יוצר JWT עם claims:
  sub          → EmployeeId (מזהה ייחודי)
  email        → אימייל
  role         → "Employee" או "Admin"
  jti          → GUID ייחודי לכל token
  firstName    → שם פרטי
  lastName     → שם משפחה
  
חתום עם HMAC-SHA256 + ה-Secret Key
תוקף: 15 דקות
```

### `GenerateRawRefreshToken`
```
יוצר 64 bytes אקראיים (cryptographically secure)
ממיר ל-Base64 string
```

### `HashToken`
```
SHA-256(rawToken) → string hex lowercase
שימוש: לשמור רק hash ב-DB, לא הטוקן עצמו
```

---

## 26. `Services/AttendanceService.cs` — לוגיקת נוכחות

**מה הוא עושה:**
כל הלוגיקה של Clock In/Out ודוחות שעות.

### `ClockInAsync`
```
1. GetZurichTimeAsync() — זמן מ-timeapi.io
2. SERIALIZABLE transaction (למניעת race condition)
3. טוען עובד מ-DB
4. בדיקה: Status == "Active"? אם לא → AccountInactiveException
5. מגדיר: todayStart = תחילת היום (00:00), tomorrowStart = תחילת המחר
6. שאילתה: מה האירוע האחרון של העובד היום?
7. אם האחרון הוא ClockIn → כבר מחובר! → InvalidOperationException
8. שומר ClockEvent חדש (EventType="ClockIn")
9. AuditLog + SaveChanges + Commit
```

**למה SERIALIZABLE?**
בלעדיו: שתי בקשות מקבילות יכולות לשתיהן לעבור שלב 7 (שניהן רואות "אין ClockIn פתוח") ושתיהן לכתוב ClockIn כפול. SERIALIZABLE נועל את הנתונים שנקראו עד לסיום ה-transaction.

### `GetReportsAsync`
```
1. טוען עובדים מ-DB (עם ClockEvents שלהם)
   אם includeInactive=false → רק Active
2. מסנן לפי חודש/שנה
3. מארגן לפי ימים (GroupBy)
4. לכל יום — בונה sessions:
   כל ClockIn שאחריו ClockOut = session שלמה
   ClockIn ללא ClockOut = session עם IsComplete=false
5. מחשב סה"כ דקות לכל יום וסה"כ לעובד
6. מחזיר ReportEntry[] עם DailyBreakdown
```

---

## 27. `Services/AdminService.cs` — לוגיקת ניהול עובדים

### `CreateEmployeeAsync`
```
1. GetZurichTimeAsync() — הזמן לשמירה ב-CreatedAt
2. מוצא Role לפי שם (Employee/Admin)
3. BCrypt.HashPassword(password, workFactor: 12) — הצפנת סיסמה
4. יוצר Employee חדש ב-DB
5. AuditLog: { action: "createEmployee", email, role }
6. מחזיר EmployeeResponse (ללא PasswordHash!)
```

### `TerminateEmployeeAsync`
```
1. מוצא עובד שStatus != "Terminated" (לא ניתן לסיים עובד שכבר סיים)
2. מגדיר Status = "Terminated"
3. AuditLog: { action: "terminateEmployee", employeeId }
4. מחזיר true (הצליח) / false (לא נמצא)
```

**שימו לב:** `TerminateEmployeeAsync` לא מבטל Refresh Tokens! זה נעשה בנפרד כי זה ב-AuthService. (שיפור אפשרי — לשנות בפרויקט אמיתי).

---

## 28. `Services/AuditService.cs` — תיעוד פעולות

**מה הוא עושה:**
שורת קוד אחת של לוגיקה — מוסיף רשומה ל-AuditLogs.

```csharp
db.AuditLogs.Add(new AuditLog {
    Action = action,              // "Login", "ClockIn", "AdminAction", ...
    EmployeeId = employeeId,      // מי ביצע
    Details = JsonSerializer.Serialize(details), // פרטים כ-JSON
    Timestamp = timestamp
});
```

**למה לא `SaveChangesAsync()`?**
כי הוא מניח שהקוד הקורא יעשה `SaveChanges` בעצמו — כך כל הפעולה (למשל ClockIn + AuditLog) נשמרת בבת אחת ב-transaction אחד.

---

## 29. `Services/TimeService.cs` — שירות הזמן

**מה הוא עושה:**
קורא לשירות חיצוני (`timeapi.io`) כדי לקבל את הזמן הנוכחי ב-Europe/Zurich.

**Polly Resilience Pipeline:**

*Retry:*
```
3 ניסיונות חוזרים עם Exponential Backoff:
  ניסיון 1 כשל → ממתין 500ms → ניסיון 2
  ניסיון 2 כשל → ממתין 1000ms → ניסיון 3
  ניסיון 3 כשל → ממתין 2000ms → מעביר לCircuit Breaker
```

*Circuit Breaker:*
```
אם 50% מהבקשות נכשלות (מינימום 3 בקשות) בתוך 30 שניות:
  Circuit OPENS → 30 שניות לא מנסים בכלל → BrokenCircuitException מיידי
  אחרי 30 שניות: Circuit HALF-OPEN → מנסה בקשה אחת
  אם הצליח → Circuit CLOSED (חוזר לנורמה)
```

**למה זה חשוב:**
בלי Circuit Breaker: כל Clock In/Out מחכה 5 שניות ואז נכשל (timeout) — המשתמש מחכה.
עם Circuit Breaker: אחרי כמה כשלונות, השרת יודע שהשירות לא זמין ומחזיר שגיאה מיידית.

---

## 30. `Services/AppMetrics.cs` — מדדים

**מה הוא עושה:**
מגדיר Counters ו-Histograms למדידת ביצועים ושגיאות.

```
ClockInAttempts    → כמה פעמים ניסו Clock In
ClockInSuccesses   → כמה הצליחו
LoginFailures      → כמה כניסות נכשלו
TimeApiFailures    → כמה פעמים timeapi.io נכשל
TimeApiLatencyMs   → כמה מילישניות כל קריאה לוקחת
RequestDurationMs  → משך כל HTTP request
```

---

## 31. `Services/TimeApiHealthMonitor.cs` — ניטור רקע

**מה הוא עושה:**
שירות שרץ ברקע (BackgroundService) ובודק את זמינות timeapi.io כל דקה.

```
בהפעלה: ממתין 30 שניות (נותן לשרת להשלים startup)
      ↓
כל דקה: מנסה GetZurichTimeAsync()
      ↓
הצליח?
  → אם היה לא זמין → "TimeAPI recovered after X min"
  → מאפס מונה כשלונות
      ↓
נכשל?
  → LogWarning: "TimeAPI unavailable — Xs elapsed"
  → אם 5 דקות ללא הפסקה → LogCritical: "[ALERT] timeAPI has been unreachable for 5 min"
```

---

## 32. `Middleware/ExceptionMiddleware.cs` — מטפל שגיאות כללי

**מה הוא עושה:**
עוטף את כל ה-request pipeline. כל שגיאה שנזרקת בכל מקום בשרת — נתפסת כאן וממירה ל-JSON מסודר.

```
TimeApiUnavailableException  → 503 + "Time service is temporarily unavailable."
AccountInactiveException     → 403 + "Your account is inactive. Contact an administrator."
DbUpdateException            → 409 + "A conflicting record already exists."
InvalidOperationException    → 400 + הודעת השגיאה
ArgumentException            → 400 + הודעת השגיאה
Exception (כל שגיאה אחרת):
  בדיבאג → 500 + "ExceptionType: message"
  בפרודקשן → 500 + "An unexpected error occurred." (לא חושפים פרטים לתוקף)
```

---

## 33. `Middleware/SecurityHeadersMiddleware.cs` — Headers אבטחה

**מה הוא עושה:**
מוסיף HTTP response headers שמגנים מפני תקיפות נפוצות.

```
X-Content-Type-Options: nosniff
  → מונע מהדפדפן לנחש סוג קובץ (מגן מ-MIME sniffing attacks)

X-Frame-Options: DENY
  → מונע הטמעת הדף ב-iframe (מגן מ-Clickjacking)

X-XSS-Protection: 1; mode=block
  → מפעיל הגנת XSS מובנית בדפדפן ישן

Referrer-Policy: strict-origin-when-cross-origin
  → שולט מה נשלח ב-Referer header

Permissions-Policy: geolocation=(), microphone=(), camera=()
  → מונע שימוש ב-APIs של מצלמה/מיקרופון/מיקום

Content-Security-Policy:
  → לנתיבי /swagger/*: מאפשר inline scripts (Swagger צריך אותם)
  → לכל שאר הנתיבים: default-src 'none' (API בלבד — לא צריך JS/CSS)
```

---

## 34. `Middleware/RequestLoggingMiddleware.cs` — לוג בקשות

**מה הוא עושה:**
מודד ומתעד כל HTTP request.

```
לפני → מתחיל stopwatch
       ↓
אחרי → מפסיק stopwatch
       ↓
מחשב: method, path, status code, duration, IP
       ↓
שומר ב-AppMetrics.RequestDurationMs
       ↓
כותב ל-log לפי severity:
  500+ → LogError
  400+ → LogWarning
  200  → LogInformation

  401  → גם: LogWarning "[AUTHN] Unauthenticated request"
  403  → גם: LogWarning "[AUTHZ] Authorization denied"
```

---

## 35. `appsettings.json` + `appsettings.Development.json`

**`appsettings.json`:**
```json
{
  "ConnectionStrings": { "Default": "" },        ← ריק! מגיע מ-Development
  "Jwt": { "Secret": "", ... },                  ← ריק! מגיע מ-Development
  "Cors": { "AllowedOrigin": "http://localhost:3000" },
  "Seed": { "AdminEmail": "admin@attendance.com", "AdminPassword": "" }
}
```

**`appsettings.Development.json`:**
```json
{
  "ConnectionStrings": { "Default": "Server=tcp:attendance-server-racheli..." },
  "Jwt": { "Secret": "..." },
  "Seed": { "AdminEmail": "admin@company.com", "AdminPassword": "Admin123!" }
}
```

**כלל חשוב:** `appsettings.Development.json` **מדרס** (override) את `appsettings.json`.
הסיסמה האמיתית לכניסה בדיבאג היא: `admin@company.com` / `Admin123!`

---

## 36. `vite.config.js` — הגדרת Proxy

**מה הוא עושה:**
מגדיר שכל בקשה מה-Frontend שמתחילה ב-`/api` תועבר אוטומטית ל-Backend.

```javascript
'/api': {
  target: 'http://localhost:5012',   ← כתובת השרת
  changeOrigin: true                 ← מוסתר מהדפדפן
}
```

**בלעדיו:** הדפדפן היה שולח בקשות ל-`http://localhost:3000/api/...` ומקבל שגיאה.
**איתו:** הבקשות הולכות ל-`http://localhost:5012/api/...` שקופית לחלוטין.

---

# סיכום — זרימה מלאה של Login עד Clock In

```
1. משתמש פותח דפדפן ל-http://localhost:3000
2. main.jsx טוען → App.jsx מנסה לנווט ל-/dashboard
3. AuthContext מנסה GET /api/auth/me → Proxy שולח ל-5012
4. לא מחובר → 401 → ProtectedRoute → redirect ל-/login
5. LoginPage מוצגת

6. משתמש מקליד admin@company.com / Admin123!
7. LoginPage.handleSubmit → ולידציה → authApi.login()
8. POST /api/auth/login → AuthController.Login()
9. AuthService.LoginAsync() → BCrypt.Verify → הצליח
10. SetCookie(access_token, refresh_token)
11. חזרה ל-AuthContext → fetchUser() → GET /api/auth/me → user מעודכן
12. navigate('/dashboard')

13. DashboardPage נטעת
14. GET /api/clock/status → ClockController.GetStatus()
15. AttendanceService.GetStatusAsync() → DB → { isClockedIn: false }
16. שעון חי מתחיל לרוץ (setInterval כל שנייה)

17. משתמש לוחץ "Clock In"
18. handleClock('in') → Debounce בדיקה → clockApi.clockIn()
19. POST /api/clock/in → ClockController.ClockIn()
20. AttendanceService.ClockInAsync():
    a. GET timeapi.io → זמן שוויץ נוכחי
    b. SERIALIZABLE transaction
    c. בדיקת עובד Active
    d. בדיקת אין ClockIn פתוח היום
    e. שמירת ClockEvent
    f. AuditLog
    g. COMMIT
21. מחזיר 200 + ClockEventResponse
22. fetchStatus() → isClockedIn = true → UI מתעדכן
23. "Clocked in successfully." + טיימר "Time elapsed" מתחיל
```

---

*המסמך הזה מכסה את כל 36 הקבצים הראשיים בפרויקט.*
