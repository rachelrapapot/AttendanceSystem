# הכנה מלאה לראיון — שאלות, תשובות, ושינויים חיים

---

# חלק א׳ — מפת הניווט: "איפה כל דבר נמצא"

## Frontend — attendance-frontend/src/

| מה לשנות | קובץ |
|----------|------|
| עמוד התחברות (מראה, טופס) | `pages/LoginPage.jsx` |
| עמוד עובד (Clock In/Out, שעון, היסטוריה) | `pages/DashboardPage.jsx` |
| עמוד ניהול (עובדים, דוחות, audit) | `pages/AdminPage.jsx` |
| סרגל ניווט | `components/NavBar.jsx` |
| מי יכול לגשת לאיזה עמוד | `components/ProtectedRoute.jsx` + `components/AdminRoute.jsx` |
| מה קורה בהתחברות/התנתקות | `contexts/AuthContext.jsx` |
| הגדרת proxy (port השרת) | `vite.config.js` |
| בקשות אימות לשרת | `api/auth.js` |
| בקשות Clock In/Out לשרת | `api/clock.js` |
| בקשות ניהול עובדים | `api/admin.js` |
| טיפול בשגיאות HTTP, 401, refresh | `api/client.js` |

## Backend — AttendanceSystem.API/

| מה לשנות | קובץ |
|----------|------|
| הגדרת השרת, Middleware, JWT | `Program.cs` |
| לוגיקת Clock In / Clock Out | `Services/AttendanceService.cs` |
| לוגיקת התחברות, refresh, logout | `Services/AuthService.cs` |
| יצירת עובד, סיום העסקה | `Services/AdminService.cs` |
| יצירת JWT token | `Services/TokenService.cs` |
| קריאה ל-timeapi.io | `Services/TimeService.cs` |
| תיעוד פעולות (AuditLog) | `Services/AuditService.cs` |
| בדיקת מורכבות סיסמה | `Filters/PasswordComplexityAttribute.cs` |
| טיפול בשגיאות גלובלי | `Middleware/ExceptionMiddleware.cs` |
| נתיבי login/logout/refresh | `Controllers/AuthController.cs` |
| נתיבי Clock In/Out | `Controllers/ClockController.cs` |
| נתיבי ניהול עובדים ודוחות | `Controllers/AdminController.cs` |
| טבלאות DB והגדרות Schema | `Data/AppDbContext.cs` |
| יצירת Admin ראשוני | `Data/SeedData.cs` |
| DTOs לאימות | `DTOs/AuthDtos.cs` |
| DTOs לנוכחות | `DTOs/ClockDtos.cs` |
| DTOs לניהול | `DTOs/AdminDtos.cs` |
| חיבור ל-DB, JWT secret, Admin password | `appsettings.Development.json` |

---

# חלק ב׳ — שאלות ותשובות לראיון

---

## נושא 1: הסבר כללי על המערכת

**ש: תסביר/י לי את הפרויקט במשפטים פשוטים.**
> מערכת נוכחות שמאפשרת לעובדים להחתים כניסה ויציאה ממשמרת. יש שני סוגי משתמשים: עובד רגיל שיכול להחתים ולראות את ההיסטוריה שלו, ומנהל שיכול לנהל עובדים ולהוציא דוחות שעות. כל הזמנים מגיעים מ-API חיצוני בשוויץ ולא מהשרת המקומי.

**ש: מה הטכנולוגיות שבחרת?**
> React עם Vite לצד הלקוח, ASP.NET Core 8 לצד השרת, Azure SQL Database כמסד נתונים, Entity Framework Core לניהול הDB, JWT בתוך httpOnly cookies לאימות.

**ש: למה דווקא Azure SQL ולא SQL Server רגיל?**
> SQL Server לא הצליח להתקין על המחשב בגלל בעיית IO alignment בחומרה (Dell Vostro עם Hypervisor). Azure SQL Free Tier פתר את הבעיה — מסד נתונים בענן, ללא התקנה מקומית, והדבר בפועל גם מדגים deployment בסביבה קרובה לפרודקשן.

---

## נושא 2: הדרישה המרכזית — timeapi.io

**ש: איך מבטיחים שהזמן מגיע מ-API חיצוני ולא מהשרת?**
> ב-`AttendanceService.cs`, השורה הראשונה בכל ClockIn ו-ClockOut היא `var now = await _timeService.GetZurichTimeAsync()`. זה קורא ל-`https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich`. רק הזמן הזה נשמר ב-DB — אין שום שימוש ב-`DateTime.Now` בתהליך הזה.

**ש: מה קורה אם timeapi.io לא עובד?**
> יש Polly Resilience Pipeline: 3 ניסיונות חוזרים עם exponential backoff (500ms, 1s, 2s). אם כולם נכשלים, Circuit Breaker נפתח ל-30 שניות ומחזיר שגיאה מיידית (503). המשתמש מקבל הודעה "Time service is temporarily unavailable." — לא ניתן לרשום נוכחות ללא שעה תקינה, זה מכוון.

**ש: ב-Frontend יש גם `timeZone: 'Europe/Zurich'` — זה לא אותו דבר?**
> לא, אלה שני דברים שונים לחלוטין. `timeapi.io` מביא את השעה האמיתית לשמירה ב-DB — זה הדרישה. `toLocaleTimeString({ timeZone: 'Europe/Zurich' })` זה רק תצוגה — ממיר את ה-timestamp השמור לפורמט שוויצרי על המסך, כדי שמשתמש מישראל יראה את השעה הנכונה ולא +1 שעה.

---

## נושא 3: אימות ואבטחה

**ש: למה cookies ולא localStorage לשמירת ה-JWT?**
> localStorage נגיש ל-JavaScript — כל קוד XSS יכול לקרוא אותו ולשלוח את הטוקן לתוקף. httpOnly cookie אינו נגיש ל-JavaScript בכלל. הדפדפן שולח אותו אוטומטית עם כל request, אבל שום script לא יכול לקרוא אותו.

**ש: מה זה SameSite=Strict ולמה?**
> SameSite=Strict אומר לדפדפן: "אל תשלח את ה-cookie הזה אם הבקשה מגיעה מאתר אחר". זה מגן מ-CSRF — תוקף לא יכול ליצור דף שגורם לדפדפן של הקורבן לשלוח בקשה לשרת שלנו עם ה-cookies.

**ש: למה שני tokens — access ו-refresh?**
> Access token תקף 15 דקות — חלון ניצול קצר אם נגנב. הוא stateless — השרת לא צריך DB לאמת אותו. Refresh token תקף 7 ימים ושמור ב-DB — ניתן לבטל אותו מיידית בלוגאוט. בלי refresh token המשתמש היה צריך להתחבר מחדש כל 15 דקות.

**ש: מה זה Refresh Token Rotation?**
> בכל שימוש ב-refresh token, הישן מבוטל וחדש ניתן. אם מישהו גנב refresh token וניסה להשתמש בו לאחר שהמשתמש האמיתי כבר השתמש בו — השרת מזהה שמנסים לעשות replay של טוקן שכבר בוטל, ומבטל את **כל** הסשנים של אותו משתמש. זה Stolen Token Detection.

**ש: למה BCrypt ולא SHA-256 לסיסמאות?**
> SHA-256 מהיר מאוד — תוקף עם GPU יכול לנסות מיליארדי ניחושים בשנייה על hash גנוב. BCrypt עם work factor 12 לוקח ~250ms לחישוב, מה שמקשה על brute force בצורה דרמטית.

**ש: איפה הגנת brute force?**
> ב-`AuthService.AlertOnBruteForceAsync`: בכל כשלון כניסה, בודק ב-AuditLogs כמה כשלונות היו לאימייל זה ב-15 דקות האחרונות. 5+ כשלונות → LogCritical. בנוסף יש rate limiting ברמת IP דרך `AspNetCoreRateLimit`.

---

## נושא 4: לוגיקה ו-Edge Cases

**ש: מה קורה אם עובד לוחץ Clock In פעמיים?**
> `AttendanceService.ClockInAsync` בודק: מה האירוע האחרון של העובד היום? אם זה ClockIn — זורק InvalidOperationException עם הודעה "Already clocked in. Clock out first." ה-ExceptionMiddleware תופס אותה ומחזיר 409.

**ש: מה זה SERIALIZABLE transaction ולמה השתמשת בו?**
> SERIALIZABLE הוא רמת הבידוד הגבוהה ביותר ב-SQL. בלעדיו: אם שני browsers שולחים Clock In באותה מיליסקנייה, שניהם בודקים "האם יש ClockIn פתוח?" ושניהם רואים "לא" — ושניהם כותבים ClockIn. SERIALIZABLE נועל את הנתונים שנקראו, כך שהשני יחכה ואז יראה שכבר יש ClockIn.

**ש: מה קורה אם עובד פוטר בזמן שהוא מחובר?**
> ה-JWT שלו תקף עוד עד 15 דקות. כשינסה Clock In/Out, `AttendanceService` בודק את ה-Status ישירות מה-DB (לא רק מה-token). אם `Status != "Active"` — זורק `AccountInactiveException` → 403 Forbidden עם הודעה ברורה. בנוסף, ה-Refresh Tokens שלו בוטלו ברגע הסיום.

**ש: מה הפרש בין Inactive ל-Terminated?**
> שניהם מונעים כניסה. Inactive = השעיה זמנית (ניתן להחזיר ל-Active). Terminated = סיום העסקה. שניהם soft delete — הרשומה נשמרת ב-DB לצורך דוחות היסטוריים.

**ש: איך עובד הדוח החודשי?**
> `AttendanceService.GetReportsAsync` טוען את כל ה-ClockEvents לפי חודש/שנה, מארגן לפי עובד ואז לפי יום, ובונה sessions — כל ClockIn+ClockOut = session אחד. ימים עם ClockIn ללא ClockOut מסומנים `isComplete: false`. המנהל רואה פירוט יומי ויכול לפתוח כל עובד לראות כל יום.

---

## נושא 5: Frontend

**ש: איך ה-AuthContext יודע אם המשתמש מחובר בטעינת הדף?**
> בטעינה שולח GET /api/auth/me. אם מחזיר 200 — המשתמש מחובר. אם 401 — מנסה POST /api/auth/refresh. אם גם זה נכשל — user = null ומועבר ל-login. הכל קורה בשקט לפני שמשהו מוצג.

**ש: מה זה Debounce ב-DashboardPage?**
> מניעת לחיצה כפולה מהירה על Clock In. אם לחצו שוב תוך 2 שניות מהלחיצה הקודמת — הבקשה לא נשלחת. זה מונע מצב של שתי בקשות Clock In שיוצאות כמעט בו-זמנית.

**ש: איך הטיימר "Time elapsed" עובד?**
> `setInterval` מגדיר חישוב כל שנייה: `now - lastClockInTime`. `now` הוא `new Date()` של הדפדפן — זה לצורך **תצוגה בלבד**, לא לרישום נוכחות. ממיר מילישניות ל-"3h 22m 15s".

**ש: מה זה Vite proxy ולמה צריך אותו?**
> הדפדפן לא יכול לשלוח בקשות ישירות מ-`localhost:3000` ל-`localhost:5012` בגלל CORS. ה-proxy של Vite מיירט כל בקשה שמתחילה ב-`/api` ומעביר אותה לשרת — שקוף לחלוטין לדפדפן.

**ש: איך הולידציה של טופס יצירת עובד עובדת?**
> שכבה כפולה: לפני שליחה — בדיקה client-side של כל שדה (שדה ריק, פורמט email, דרישות סיסמה). אם ה-API מחזיר שגיאות — `mapServerErrors` ממיר PascalCase C# לcamelCase JS ומציג כל שגיאה ליד השדה הנכון.

---

## נושא 6: מסד נתונים

**ש: למה Code-First ולא Database-First?**
> ה-Schema מוגדר בקוד C# ומנוהל דרך Migration files ב-git. כל שינוי מתועד, reversible, ועובר Code Review. עם Database-First הDB קובע את הקוד — פחות גמישות.

**ש: מה זה CHECK constraint ולמה?**
> הגבלה ברמת ה-DB שמונעת ערכים לא חוקיים. למשל: `Status IN ('Active', 'Inactive', 'Terminated')` — גם אם יהיה bug בקוד שמנסה לשמור `Status = "Disabled"`, ה-DB יזרוק שגיאה. הגנה כפולה.

**ש: למה לא לשמור את ה-Refresh Token עצמו?**
> Defense in Depth: אם מישהו יגנוב גיבוי של ה-DB, הוא יראה רק SHA-256 hashes — לא ניתן לשחזר את הטוקן המקורי מה-hash. בדיוק כמו שמורים password hash ולא סיסמה.

**ש: למה AuditLog.EmployeeId הוא nullable?**
> כדי שרשומת audit לא תימחק כשעובד מסיים. `ON DELETE SET NULL` — אם עובד נמחק, ה-EmployeeId הופך NULL אבל הרשומה ההיסטורית נשארת.

---

## נושא 7: ארכיטקטורה והחלטות

**ש: למה הפרדת ל-Services ולא שמת הכל ב-Controller?**
> Separation of Concerns. Controller אחראי על HTTP — קריאת request, החזרת response. Service אחראי על עסקים — הלוגיקה. זה מקל על בדיקות (אפשר לבדוק Service ללא HTTP) ועל תחזוקה.

**ש: מה זה Dependency Injection ואיך זה עובד כאן?**
> במקום שכל class יצור את התלויות שלו, ה-framework מזריק אותן. ב-`Program.cs` מגדירים `AddScoped<IAttendanceService, AttendanceService>()` — כשמישהו צריך `IAttendanceService`, ASP.NET יוצר `AttendanceService` ומזריק אותו אוטומטית ל-constructor.

**ש: מה ההבדל בין Singleton, Scoped, ו-Transient?**
> Singleton = instance אחד לכל חיי האפליקציה (AppMetrics). Scoped = instance חדש לכל HTTP request (AuthService, AttendanceService). Transient = instance חדש בכל פעם שמבקשים.

**ש: למה CORS מוגדר רק ל-http://localhost:3000?**
> הגנת אבטחה — רק הFrontend שלנו יכול לשלוח בקשות לAPI. אתר חיצוני שינסה לקרוא לAPI שלנו יקבל שגיאה CORS מהדפדפן.

---

# חלק ג׳ — שינויים חיים שהמראיין יכול לבקש

---

## שינוי 1: הוסף שדה "Phone Number" לעובד

**Backend — 3 מקומות:**

**`DTOs/AdminDtos.cs`** — הוסף שדה:
```csharp
public record CreateEmployeeRequest(
    // ... שדות קיימים ...
    [MaxLength(20)] string? PhoneNumber  // ← הוסף
);
```

**`Models/Employee.cs`** — הוסף למודל:
```csharp
public string? PhoneNumber { get; set; }
```

**`Services/AdminService.cs`** — הוסף ב-CreateEmployeeAsync:
```csharp
var employee = new Employee
{
    // ... שדות קיימים ...
    PhoneNumber = request.PhoneNumber  // ← הוסף
};
```

**Frontend — `pages/AdminPage.jsx`** — הוסף לטופס:
```jsx
<div className="form-group">
  <label>Phone Number</label>
  <input value={form.phoneNumber} onChange={updateForm('phoneNumber')} 
         placeholder="050-1234567" />
</div>
```

---

## שינוי 2: שנה את תוקף ה-JWT מ-15 דקות ל-30 דקות

**קובץ:** `Controllers/AuthController.cs` שורה 51:
```csharp
// לפני:
SetCookie(AccessTokenCookie, accessToken, DateTime.UtcNow.AddMinutes(15));

// אחרי:
SetCookie(AccessTokenCookie, accessToken, DateTime.UtcNow.AddMinutes(30));
```

**קובץ:** `Services/TokenService.cs` שורה 44:
```csharp
// לפני:
expires: DateTime.UtcNow.AddMinutes(15),

// אחרי:
expires: DateTime.UtcNow.AddMinutes(30),
```

---

## שינוי 3: הוסף הודעת אישור לפני Clock Out

**קובץ:** `pages/DashboardPage.jsx` בתוך `handleClock`:
```jsx
const handleClock = async (action) => {
    // הוסף:
    if (action === 'out') {
        const confirmed = window.confirm('Are you sure you want to clock out?');
        if (!confirmed) return;
    }
    // שאר הקוד נשאר...
```

---

## שינוי 4: הוסף כפתור Export CSV לדוח

**קובץ:** `pages/AdminPage.jsx` — הוסף פונקציה ואז כפתור:
```jsx
const exportCsv = () => {
    const rows = [['Employee', 'Date', 'Clock In', 'Clock Out', 'Hours']];
    reports.forEach(r => {
        r.dailyBreakdown?.forEach(d => {
            rows.push([
                r.fullName, d.date,
                d.sessions[0]?.clockIn ? formatTimestamp(d.sessions[0].clockIn) : '',
                d.sessions[0]?.clockOut ? formatTimestamp(d.sessions[0].clockOut) : '',
                formatMinutes(d.totalMinutesWorked)
            ]);
        });
    });
    const csv = rows.map(r => r.join(',')).join('\n');
    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `report_${reportYear}_${reportMonth}.csv`;
    a.click();
};
```
ואז הוסף כפתור ליד ה-filter:
```jsx
<button className="btn btn-primary btn-sm" onClick={exportCsv}>Export CSV</button>
```

---

## שינוי 5: שנה את הודעת שגיאה כשתוקף הסיסמה נכשל

**קובץ:** `Filters/PasswordComplexityAttribute.cs`:
```csharp
// לפני:
if (!password.Any(char.IsUpper))
    return new ValidationResult("Password must contain at least one uppercase letter.");

// אחרי — שנה לעברית או לכל הודעה אחרת:
if (!password.Any(char.IsUpper))
    return new ValidationResult("הסיסמה חייבת להכיל לפחות אות גדולה אחת באנגלית.");
```

---

## שינוי 6: הוסף חיפוש לדוחות לפי שם עובד

**קובץ:** `pages/AdminPage.jsx` — הוסף state:
```jsx
const [reportSearch, setReportSearch] = useState('');
```

הוסף input מעל הטבלה:
```jsx
<input 
    type="search" 
    placeholder="Search employee..." 
    value={reportSearch} 
    onChange={(e) => setReportSearch(e.target.value)} 
/>
```

סנן את ה-reports:
```jsx
const filteredReports = reports.filter(r => 
    !reportSearch || r.fullName.toLowerCase().includes(reportSearch.toLowerCase())
);
// ואז בטבלה: filteredReports.map(...) במקום reports.map(...)
```

---

## שינוי 7: שנה את צבע הכפתורים

**קבצי CSS:** `attendance-frontend/src/index.css`

```css
/* חפש: */
.action-btn-in { background: ... }
.action-btn-out { background: ... }

/* שנה לכל צבע שרוצים */
```

---

## שינוי 8: הוסף הגבלה — עובד לא יכול להחתים יותר מ-16 שעות ביום

**קובץ:** `Services/AttendanceService.cs` ב-`ClockOutAsync`, לפני שמירת האירוע:

```csharp
// הוסף לפני: _db.ClockEvents.Add(clockEvent);
var firstClockIn = dayEvents.First(e => e.EventType == "ClockIn");
var totalMinutes = (int)(now - firstClockIn.Timestamp).TotalMinutes;
if (totalMinutes > 16 * 60)
    throw new InvalidOperationException(
        "Cannot clock out: shift exceeds 16 hours. Please contact an administrator.");
```

---

# חלק ד׳ — שאלות "הסבר לי את הקוד הזה"

**ש: תסביר/י לי את הקוד הזה:**
```csharp
await using var tx = _db.Database.IsRelational()
    ? await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable)
    : (IDbContextTransaction?)null;
```

> בודק אם מסד הנתונים הוא relational (SQL Server) או InMemory (לבדיקות). SQL Server תומך ב-transactions, InMemory לא — אז אם זה InMemory מגדירים null. `await using` מבטיח שה-transaction יסגר אוטומטית בסוף הבלוק.

---

**ש: תסביר/י לי:**
```javascript
const [user, setUser] = useState(null);
```

> `useState` ב-React יוצר משתנה state. `user` = הערך הנוכחי. `setUser` = פונקציה לשינוי. כשקוראים ל-`setUser(newValue)` — React מרנדר מחדש את כל הרכיבים שמשתמשים ב-`user`.

---

**ש: תסביר/י לי:**
```javascript
const loadData = useCallback(async () => { ... }, [reportMonth, reportYear, includeInactive]);
useEffect(() => { loadData(); }, [loadData]);
```

> `useCallback` יוצר פונקציה שנוצרת מחדש רק כשהתלויות משתנות. `useEffect` קורא ל-`loadData` בכל פעם שהיא משתנה — כלומר בכל פעם שמשתנה חודש/שנה/includeInactive, הדוח נטען מחדש.

---

**ש: תסביר/י לי:**
```csharp
if (stored.RevokedAt != null)
{
    var allTokens = await _db.RefreshTokens
        .Where(rt => rt.EmployeeId == stored.EmployeeId && rt.RevokedAt == null)
        .ToListAsync();
    foreach (var t in allTokens) t.RevokedAt = now;
```

> זה Stolen Token Detection. אם מישהו ניסה להשתמש בRefresh Token שכבר בוטל — זה סימן שהטוקן נגנב ומישהו מנסה לנצל אותו. התגובה: ביטול **כל** הסשנים הפעילים של אותו משתמש — גרוש מכל המכשירים.

---

# חלק ה׳ — שאלות "מה היית עושה אחרת / מה חסר"

**ש: מה היית מוסיף/ה אם היה עוד זמן?**
> 1. Pagination לטבלאות (כרגע נטענות כל הרשומות). 2. שינוי סיסמה עצמית לעובד. 3. Email notifications למנהל על חריגות (עובד לא יצא 16 שעות). 4. בדיקות integration מקיפות. 5. Docker Compose להרצה קלה.

**ש: מה הבאג הכי גדול שיכול להיות?**
> אם timeapi.io מחזיר זמן שגוי (פחות סביר אך אפשרי). לא יש לנו ולידציה שה-timestamp סביר. ניתן להוסיף בדיקה: אם הזמן שחזר שונה ביותר מ-5 דקות מ-DateTime.UtcNow — לוג אזהרה.

**ש: איך היית עושה בדיקות (tests) לפרויקט?**
> Integration tests עם WebApplicationFactory ו-EF InMemory: בדיקת Flow מלא (login → clockIn → clockOut → getReports). Unit tests ל-AttendanceService עם Mock של ITimeService. Tests ל-boundary: double clockIn → 409, clockOut ללא clockIn → 409, עובד Terminated → 403.

---

# חלק ו׳ — טיפים להצגה עצמה

## פתיחה מומלצת (30 שניות)
> "בניתי מערכת נוכחות עם React ו-ASP.NET Core. הדגש המרכזי הוא שכל החתמה מתבססת על שעה מ-API חיצוני בשוויץ. המערכת כוללת שני תפקידים — עובד ומנהל — עם authentication מאובטח דרך JWT בcookies, דוחות שעות חודשיים, ויומן ביקורת."

## מה להדגים בDEMO
1. **Swagger** — `http://localhost:5012/swagger` — מרשים, מראה תיעוד מלא
2. **Login** — הראה cookies ב-DevTools (F12 → Application → Cookies)
3. **Clock In/Out** — הראה שהשעה מגיעה מ-timeapi.io בלוגים של השרת
4. **Admin panel** — צור עובד, הדגם ולידציה (נסה סיסמה חלשה), הראה דוח
5. **Error handling** — נסה Clock In פעמיים — הראה הודעת שגיאה ברורה

## איפה להסתכל ב-DevTools
- **Cookies**: F12 → Application → Cookies → localhost — רואים access_token + refresh_token
- **Network**: F12 → Network — רואים כל request ותגובה
- **Console**: F12 → Console — הודעות שגיאה אם יש

## תשובה לכל שאלה שלא יודעים
> "ההחלטה הספציפית הזו לא הייתה בסדר העדיפויות שלי, אבל הגישה שהייתי בוחר היא [הכנס הגיון כללי]. ניתן לממש אותה ב-[קובץ רלוונטי]."

---

*עדכון: 2026-06-08*
