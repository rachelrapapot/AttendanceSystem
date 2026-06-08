# שאלות ותשובות לראיון — לפי מה ששאלו בפועל

---

## 1. מה זה Lambda Function ב-C#?

**תשובה:**
Lambda היא פונקציה קצרה ואנונימית (ללא שם) שכותבים inline במקום להגדיר מתודה נפרדת.

```csharp
// פונקציה רגילה:
bool IsAdult(int age) { return age >= 18; }

// אותו דבר כ-Lambda:
Func<int, bool> isAdult = age => age >= 18;
```

**הסימן:** `=>` נקרא "arrow" או "goes to"

**דוגמה מהפרויקט שלי:**
```csharp
// AttendanceService.cs
var employees = await query
    .OrderBy(e => e.LastName)    // lambda: לקחת עובד ולהחזיר LastName
    .ThenBy(e => e.FirstName)    // lambda
    .ToListAsync();

// AppDbContext.cs
var employee = await _db.Employees
    .FirstOrDefaultAsync(e => e.Email == request.Email && e.Status == "Active");
    // lambda: e => ... זה פונקציה שמקבלת employee ומחזירה true/false
```

**שאלות נוספות שיכול לשאול:**
- מה ההבדל בין Lambda ל-Delegate?
- מה זה `Func<T>` ו-`Action<T>`?
- מתי כדאי להשתמש ב-Lambda?

---

## 2. מה זה Protected ב-C#?

**תשובה:**
`protected` הוא **access modifier** — קובע מי יכול לגשת למתודה/שדה.

| Modifier | מי יכול לגשת |
|----------|-------------|
| `public` | כולם |
| `private` | רק אותה מחלקה |
| `protected` | אותה מחלקה + כל מחלקה שיורשת ממנה |
| `internal` | כל הקוד באותו פרויקט |

```csharp
class Animal {
    public string Name { get; set; }
    private string _secret = "hidden";
    protected void Breathe() { /* רק Animal וילדיו יכולים לקרוא */ }
}

class Dog : Animal {
    public void Bark() {
        Breathe();   // ✓ עובד — Dog יורש מ-Animal
        // _secret;  // ✗ שגיאה — private
    }
}
```

**שאלות נוספות:**
- מה ההבדל בין `private` ל-`protected`?
- מה זה inheritance (ירושה)?
- מה זה `override`?

---

## 3. מה זה Hooks ב-React? תן דוגמאות

**תשובה:**
Hooks הם פונקציות מיוחדות ב-React שמתחילות ב-`use`. הן מאפשרות לרכיב function לנהל state ולגשת לפיצ'רים של React.

**ה-Hooks הכי חשובים:**

### `useState` — שמירת מידע
```javascript
const [count, setCount] = useState(0);
// count = הערך הנוכחי
// setCount = פונקציה לשנות את הערך
// 0 = ערך ברירת מחדל
```

### `useEffect` — פעולה שתרוץ כשמשהו קורה
```javascript
useEffect(() => {
    fetchData();        // הפעולה
}, [userId]);           // תרוץ כשuserId משתנה
```

### `useContext` — גישה למידע גלובלי
```javascript
const { user, logout } = useAuth(); // מ-AuthContext
```

### `useCallback` — שמירת פונקציה
```javascript
const loadData = useCallback(async () => { ... }, [month, year]);
```

### `useRef` — גישה לאלמנט DOM / ערך שלא גורם לrender
```javascript
const lastClickRef = useRef(0); // בפרויקט — למניעת לחיצה כפולה
```

**דוגמאות מהפרויקט שלי:**
```javascript
// DashboardPage.jsx
const [now, setNow] = useState(() => new Date());  // useState
const { user } = useAuth();                         // useContext
const lastClickRef = useRef(0);                     // useRef

useEffect(() => {
    const id = setInterval(() => setNow(new Date()), 1000);
    return () => clearInterval(id);  // cleanup
}, []);
```

**שאלות נוספות:**
- מה ההבדל בין `useState` ל-`useRef`?
- מה זה cleanup function ב-useEffect?
- מתי משתמשים ב-`useMemo`?

---

## 4. הרחב על useEffect

**תשובה:**
`useEffect` מריץ קוד בעקבות שינוי. יש לו שלושה מצבים:

```javascript
// 1. רץ אחרי כל render
useEffect(() => {
    console.log('rendered!');
});

// 2. רץ פעם אחת בטעינה ([] ריק = אין תלויות)
useEffect(() => {
    fetchInitialData();
}, []);

// 3. רץ כשהתלות משתנה
useEffect(() => {
    fetchUserData();
}, [userId]);  // רץ מחדש כשuserId משתנה
```

**Cleanup function:**
```javascript
useEffect(() => {
    const interval = setInterval(() => setNow(new Date()), 1000);

    return () => clearInterval(interval); // cleanup — רץ לפני ה-unmount
}, []);
```
**למה cleanup?** כדי לא לגרום memory leaks — אם הרכיב נסגר אבל ה-interval ממשיך לרוץ.

**דוגמה מהפרויקט:**
```javascript
// DashboardPage.jsx — טוען סטטוס בטעינה
useEffect(() => { fetchStatus(); }, [fetchStatus]);

// טוען היסטוריה רק כשעוברים לטאב History
useEffect(() => {
    if (activeTab === 'history') fetchHistory();
}, [activeTab, fetchHistory]);
```

---

## 5. מה זה API?

**תשובה:**
API = **Application Programming Interface** — דרך לשתי מערכות לדבר אחת עם השנייה.

**דוגמה מהחיים:**
מלצר במסעדה = API. אתה (הלקוח = Frontend) אומר למלצר מה אתה רוצה. המלצר מעביר לשף (Backend). השף מכין ומחזיר. אתה לא צריך לדעת איך השף עובד.

**REST API** הוא הסוג הנפוץ ביותר:
```
GET    /api/employees      → קבל רשימת עובדים
POST   /api/employees      → צור עובד חדש
PUT    /api/employees/5    → עדכן עובד מספר 5
DELETE /api/employees/5    → מחק עובד מספר 5
```

**בפרויקט שלי:**
```
POST /api/auth/login      → התחברות
POST /api/clock/in        → החתמת כניסה
GET  /api/admin/reports   → דוח שעות
```

**שאלות נוספות:**
- מה ההבדל בין GET ל-POST?
- מה זה HTTP Status Codes (200, 400, 401, 404, 500)?
- מה זה JSON?
- מה זה REST?

---

## 6. איך עושים API Call ב-C#?

**תשובה:**
משתמשים ב-`HttpClient`:

```csharp
// דרך 1 — פשוטה
using var client = new HttpClient();
var response = await client.GetAsync("https://api.example.com/data");
var json = await response.Content.ReadAsStringAsync();

// דרך 2 — עם Dependency Injection (הדרך הנכונה)
public class TimeService
{
    private readonly HttpClient _httpClient;

    public TimeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetDataAsync()
    {
        var response = await _httpClient.GetAsync("https://api.example.com");
        response.EnsureSuccessStatusCode(); // זורק שגיאה אם לא 200
        return await response.Content.ReadAsStringAsync();
    }
}
```

**דוגמה מהפרויקט שלי — TimeService.cs:**
```csharp
var response = await _httpClient.GetAsync(
    "https://timeapi.io/api/time/current/zone?timeZone=Europe/Zurich");
response.EnsureSuccessStatusCode();
var json = await response.Content.ReadAsStringAsync();
using var doc = JsonDocument.Parse(json);
var dateTime = doc.RootElement.GetProperty("dateTime").GetString();
```

**שאלות נוספות:**
- מה ההבדל בין `GetAsync` ל-`PostAsync`?
- מה זה `async/await`?
- מה זה `HttpClientFactory`?

---

## 7. איך את עובדת עם AI?

**תשובה:**
אני משתמשת ב-Claude Code (Anthropic) לאורך כל הפיתוח. ה-AI עוזר ל:
- **יצירת קוד** — מסביר מה אני צריכה, הוא כותב
- **Debug** — מדביקה שגיאה, הוא מסביר מה לא בסדר ומציע פתרון
- **הסבר** — "מה זה SERIALIZABLE transaction?" — מקבלת הסבר מותאם לפרויקט
- **Code Review** — "האם יש בעיות אבטחה בקוד הזה?"
- **Refactoring** — "שפר את הפונקציה הזו"

**הגישה הנכונה לעבודה עם AI:**
1. להבין מה הAI כתב — לא רק להדביק
2. לשאול "למה?" ולא רק "מה?"
3. לבדוק שהקוד עובד ולא להניח שהוא תמיד צודק
4. להשתמש בו כ-"עמית מנוסה" שזמין תמיד

---

## 8. מה זה PR?

**תשובה:**
PR = **Pull Request** — בקשה למזג שינויי קוד שלך ל-branch הראשי.

**הזרימה:**
```
main branch (הקוד הייצורי)
      ↓
יוצרת branch חדש: feature/add-export-csv
      ↓
כותבת קוד, עושה commits
      ↓
פותחת PR: "בקשה למזג את branch שלי ל-main"
      ↓
חבר צוות עושה Code Review
      ↓
מאשרים → Merge ← הקוד עובר ל-main
```

**למה PR ולא ישר push ל-main?**
Code Review — לפחות עוד אדם בודק את הקוד לפני שהוא הולך לפרודקשן. מוצא bugs, בעיות אבטחה, ומשפר quality.

**שאלות נוספות:**
- מה זה git merge vs rebase?
- מה זה conflict ב-git?
- מה ההבדל בין `git pull` ל-`git fetch`?

---

## 9. מה ההבדל בין Interface ל-Class?

**תשובה:**

| | Class | Interface |
|--|-------|-----------|
| **מה זה** | תבנית לאובייקט עם מימוש | חוזה — מה צריך להיות |
| **יש מימוש קוד?** | כן | לא (רק הגדרות) |
| **ירושה** | יכולה לרשת מ-class אחד | יכולה לממש כמה interfaces |
| **יצירת instance** | כן: `new Dog()` | לא |

```csharp
// Interface — חוזה
public interface IAnimal {
    string Name { get; }
    void MakeSound(); // רק הגדרה, אין קוד
}

// Class — מימוש
public class Dog : IAnimal {
    public string Name { get; } = "Rex";
    public void MakeSound() { Console.WriteLine("Woof!"); } // קוד אמיתי
}

public class Cat : IAnimal {
    public string Name { get; } = "Kitty";
    public void MakeSound() { Console.WriteLine("Meow!"); }
}
```

**למה Interface?**
- אפשר להחליף מימוש בלי לשנות את הקוד שמשתמש בו
- מאפשר בדיקות (Mocking)
- כפיית "חוזה" על כל מי שמממש

**דוגמה מהפרויקט:**
```csharp
// Program.cs
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
// IAttendanceService = interface (החוזה)
// AttendanceService = class (המימוש)
// בבדיקות אפשר להחליף ל-MockAttendanceService
```

**שאלות נוספות:**
- מה זה Abstract Class ומה ההבדל מ-Interface?
- מה זה Polymorphism?
- מה זה Dependency Injection?

---

## 10. מחלקה סטטית — מה זה ולמה לא עושים הכל סטטי?

**תשובה:**

**מחלקה סטטית:**
```csharp
public static class MathHelper {
    public static int Add(int a, int b) => a + b;
}
// שימוש: MathHelper.Add(3, 5) — לא צריך new MathHelper()
```

**למה לא עושים הכל סטטי?**

1. **אין instance → אין state** — מחלקה סטטית לא יכולה לשמור נתונים שונים לכל שימוש
```csharp
// לא אפשרי עם static:
var user1 = new UserService("Alice"); // state = "Alice"
var user2 = new UserService("Bob");   // state = "Bob"
```

2. **לא ניתן לבדוק (Testing)** — לא אפשר ל-"mock" מחלקה סטטית
3. **לא ניתן לירושה** — מחלקה סטטית לא יכולה לממש interface
4. **בעיות ב-multithreading** — state משותף בין כל ה-threads

**מתי כן משתמשים?**
פעולות עזר שלא צריכות state:
```csharp
// PasswordComplexityAttribute.cs בפרויקט שלי:
private static readonly HashSet<string> CommonPasswords = new(...);
// סטטי כי הרשימה זהה לכולם ולא משתנה
```

---

## 11. אינטרפייס לממשק — איך מחזירים יוזר שנרשם ראשון?

**תשובה:**
זו שאלת LINQ. `FirstOrDefault()` מחזיר את הראשון לפי סדר.

```csharp
// לפי CreatedAt — הישן ביותר ראשון
var firstUser = await _db.Employees
    .OrderBy(e => e.CreatedAt)
    .FirstOrDefaultAsync();

// לפי ID — הנמוך ביותר ראשון
var firstUser = await _db.Employees
    .OrderBy(e => e.EmployeeId)
    .FirstOrDefaultAsync();
```

**מה זה `FirstOrDefault`?**
- `First()` — מחזיר את הראשון. אם אין תוצאות → זורק שגיאה
- `FirstOrDefault()` — מחזיר את הראשון. אם אין תוצאות → מחזיר `null`

**שאלות נוספות:**
- מה ההבדל בין `First` ל-`Single`?
- מה זה LINQ?
- איך מסננים ב-LINQ? (`Where`)

---

## 12. JWT — איך רואים שהטוקן נשלח? תראי לי בפרויקט

**תשובה + איפה לראות בפרויקט:**

**איפה הטוקן נשמר:**
```
F12 (DevTools) → Application → Cookies → localhost:3000
שם תראו:
  access_token  = eyJhbGciOiJIUzI1NiIs...  (JWT)
  refresh_token = abc123xyz...               (opaque token)
```

**איפה הוא נשלח — `client.js`:**
```javascript
credentials: 'include'  // ← זה מה שגורם לדפדפן לשלוח את ה-cookies
```

**איפה הוא מוגדר — `AuthController.cs`:**
```csharp
Response.Cookies.Append("access_token", accessToken, new CookieOptions
{
    HttpOnly = true,     // JavaScript לא יכול לקרוא
    Secure = false,      // בdev — HTTP מותר
    SameSite = SameSiteMode.Strict,
    Expires = DateTime.UtcNow.AddMinutes(15)
});
```

**איפה הוא נקרא — `Program.cs`:**
```csharp
opts.Events = new JwtBearerEvents
{
    OnMessageReceived = ctx =>
    {
        var token = ctx.Request.Cookies["access_token"]; // קורא מה-cookie
        if (!string.IsNullOrEmpty(token))
            ctx.Token = token;
        return Task.CompletedTask;
    }
};
```

**איפה לראות בNetork:**
F12 → Network → כל request → Headers → Request Headers → `Cookie: access_token=eyJ...`

---

## 13. מה זה קובץ MD?

**תשובה:**
MD = **Markdown** — שפת סימון פשוטה לכתיבת מסמכים.

```markdown
# כותרת גדולה
## כותרת בינונית
**טקסט מודגש**
*טקסט נטוי*
- פריט ברשימה
- פריט נוסף
```

**שימושים:**
- `README.md` — תיעוד של פרויקט ב-GitHub
- `CHANGELOG.md` — רשימת שינויים
- תיעוד כללי, הוראות התקנה

**בפרויקט שלי:**
יצרתי `PROJECT_GUIDE.md`, `PAGES_EXPLAINED.md`, `INTERVIEW_PREP.md` — כולם קבצי Markdown לתיעוד.

---

## 14. איך מגדירים Skills ב-Claude ומה השימושים?

**תשובה:**
Skills ב-Claude Code הם "slash commands" מותאמים אישית — פקודות שמגדירים פעם אחת ומשתמשים בהן שוב.

```
/code-review    → בדיקת קוד
/test           → כתיבת בדיקות
/explain        → הסבר קוד
```

**שימושים נוספים ל-Claude:**
- כתיבת קוד מאפס
- Debug — "מה הבעיה בקוד הזה?"
- Code Review — "האם יש בעיות?"
- הסבר מושגים — "מה זה SOLID?"
- כתיבת בדיקות
- refactoring
- כתיבת תיעוד (README, comments)
- תרגום קוד בין שפות

---

## 15. מה זה `using` ב-C#?

**תשובה:**
ל-`using` יש שני שימושים שונים:

### שימוש 1 — Import (ייבוא namespace)
```csharp
using System.Text.Json;       // כמו import ב-JavaScript
using Microsoft.EntityFrameworkCore;

// בלי זה היה צריך לכתוב:
System.Text.Json.JsonDocument.Parse(json);
// עם using פשוט:
JsonDocument.Parse(json);
```

### שימוש 2 — IDisposable (ניהול משאבים)
```csharp
using var connection = new SqlConnection(connStr);
// כשנגמר הבלוק → connection.Dispose() נקרא אוטומטית
// משחרר את ה-connection חזרה ל-pool
```

**בלי `using`:**
```csharp
var connection = new SqlConnection(connStr);
try {
    // קוד
} finally {
    connection.Dispose(); // חייב לקרוא ידנית
}
```

**דוגמה מהפרויקט:**
```csharp
// AttendanceService.cs
await using var tx = await _db.Database.BeginTransactionAsync(...);
// tx.DisposeAsync() נקרא אוטומטית בסוף — גם אם יש שגיאה
```

---

## 16. מה ההבדל בין `async/await` לקוד רגיל?

**תשובה:**

**קוד סינכרוני (חוסם):**
```csharp
// ה-thread מחכה ולא עושה כלום
var result = LongOperation(); // חוסם 5 שניות
Console.WriteLine(result);
```

**קוד אסינכרוני (לא חוסם):**
```csharp
// ה-thread משוחרר בזמן ההמתנה
var result = await LongOperationAsync(); // מחכה אבל לא חוסם
Console.WriteLine(result);
```

**למה חשוב?**
שרת שמטפל ב-1000 בקשות במקביל — אם כל בקשה חוסמת thread בזמן המתנה לDB, נגמרים ה-threads. עם `async/await` ה-thread משוחרר ויכול לטפל בבקשות אחרות בזמן ההמתנה.

**בפרויקט שלי — כמעט כל מתודה:**
```csharp
public async Task<ClockEventResponse> ClockInAsync(...)
{
    var now = await _timeService.GetZurichTimeAsync();    // מחכה ל-API
    var employee = await _db.Employees.FirstOrDefaultAsync(...); // מחכה ל-DB
    await _db.SaveChangesAsync();                         // מחכה לשמירה
}
```

---

## 17. מה ההבדל בין `null` ל-`""` (string ריק)?

```csharp
string a = null;  // המשתנה לא מצביע לכלום
string b = "";    // המשתמש קיים אבל ריק
string c = " ";   // המשתנה קיים עם רווח

string.IsNullOrEmpty(a)     // true
string.IsNullOrEmpty(b)     // true
string.IsNullOrEmpty(c)     // false!
string.IsNullOrWhiteSpace(c) // true — בודק גם רווחים
```

---

## 18. מה זה `List` לעומת `Array`?

```csharp
// Array — גודל קבוע
int[] arr = new int[5];    // 5 תאים בדיוק
arr[0] = 1;

// List — גודל דינמי
List<int> list = new List<int>();
list.Add(1);
list.Add(2);
list.Remove(1);
Console.WriteLine(list.Count);  // 1
```

**בפרויקט:**
```csharp
// AdminService.cs
public async Task<List<EmployeeResponse>> GetEmployeesAsync()
// מחזיר List כי לא יודעים מראש כמה עובדים יש
```

---

## 19. מה זה try/catch?

```csharp
try {
    var result = RiskyOperation(); // קוד שיכול לזרוק שגיאה
}
catch (NotFoundException ex) {
    // טיפול בשגיאה ספציפית
    return NotFound(ex.Message);
}
catch (Exception ex) {
    // טיפול בכל שגיאה אחרת
    return StatusCode(500);
}
finally {
    // רץ תמיד — גם אם יש שגיאה, גם אם אין
    Cleanup();
}
```

**בפרויקט — ExceptionMiddleware.cs:**
```csharp
try {
    await next(context);  // כל ה-request
}
catch (TimeApiUnavailableException ex) {
    await WriteJson(context, 503, ex.Message);
}
catch (InvalidOperationException ex) {
    await WriteJson(context, 400, ex.Message);
}
```

---

## 20. שאלות על "יום בעבודה" — תשובה מומלצת

**תשובה לדוגמה:**
> "אני מתחילה בסקירת ה-PR comments שנשארו מאתמול ובמה שצריך לתקן. אחר כך Daily standup עם הצוות — מה עשיתי אתמול, מה היום, האם יש blockers. אחרי זה עבודה על המשימה הנוכחית: קוראת את הגדרת הfeature, מתכננת, כותבת קוד, בודקת. אם נתקעת — קודם מנסה לפתור לבד, אחר כך AI, אחר כך שואלת עמית. בסוף יום — commit, פתיחת PR, תיאור מה שינויתי ולמה."

---

## טיפים אחרונים

**אם לא יודעים תשובה:**
> "לא נתקלתי בזה ישירות, אבל ההיגיון שלי אומר שזה קשור ל-[ניחוש מושכל]. אשמח להעמיק בזה."

**להביא הכל לפרויקט:**
כמעט לכל שאלה — יש דוגמה בפרויקט. "הנה איפה זה מופיע אצלי: ..."

**לא לשכוח:**
- לנשום
- לחשוב בקול — "אני חושבת שזה..."
- לשאול אם השאלה לא ברורה: "אתה מתכוון ל...?"
