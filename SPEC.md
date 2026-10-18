# SPEC – מערכת השאלת מחשבים

מסמך האפיון. הוא עונה על השאלה **"מה בונים"**, ו-`PLAN.md` עונה על **"באיזה סדר"**.
דרישות הקורס המלאות נמצאות ב-`REQUIREMENTS.md`.

## כללים קבועים (להעתיק ל-`.cursor/rules`)

- 4 פרויקטים + Tests: `Core` (Entities, Interfaces, DTOs, Enums, Result), `Data` (DbContext, Repositories, Migrations), `Service` (לוגיקה עסקית), `API` (Controllers, Middleware, Program.cs).
- `Core` לא מפנה לשום פרויקט או חבילה חיצונית. אסור EF Core ב-Core.
- Controller לא מזריק DbContext או Repository, רק Service. ה-Controller דק: קורא ל-Service ומתרגם `Result` לסטטוס HTTP.
- ולידציה עסקית (שתלויה במצב הנתונים) יושבת ב-Service. ולידציה של צורת הקלט היא Data Annotations על ה-DTOs.
- כל הקוד אסינכרוני עם `CancellationToken` לכל אורך השרשרת. אסור `.Result` ו-`.Wait()`.
- ישויות לא יוצאות ולא נכנסות דרך ה-API, רק DTOs (Request ו-Response נפרדים) עם AutoMapper Profiles.
- כל התלויות מוזרקות דרך ה-constructor. אין `new` על service בתוך service. ה-interfaces ב-Core, המימושים ב-Data וב-Service, והרישום ב-`Program.cs`.
- שאילתות קריאה עם `AsNoTracking()`. pagination עם `Skip`/`Take` בתוך השאילתה. בלי N+1: משתמשים ב-`Include`/`ThenInclude`.
- ה-connection string ומפתח ה-JWT נשמרים ב-`ComputerLending.API/appsettings.json`, כדי שהפרויקט ירוץ בלי User Secrets.
- כל התאריכים `DateTime.UtcNow` (Kind=Utc), בלי `DateTime.Now`.
- לוגים עם `ILogger` (NLog מתחת): Debug לפיתוח, Information לזרימה, Warning לחריג שטופל (כולל התנגשות על משאב), Error לכשל. בלי סיסמאות, tokens או גוף בקשת הרשמה.
- מסד נתונים: PostgreSQL (Npgsql), Code-First.

---

## Enums

- `UserRole`: `Admin`, `Borrower`
- `ComputerStatus`: `Available`, `Loaned`, `InMaintenance`
- `LoanStatus`: `Active`, `Returned`

(נשמרים ב-DB כ-string.)

## ישויות

### User
| שדה | טיפוס | הערות |
|---|---|---|
| Id | int | PK |
| FullName | string | חובה, עד 100 תווים |
| Email | string | חובה, ייחודי |
| PasswordHash | string | BCrypt |
| Role | UserRole | |
| CreatedAtUtc | DateTime | |
| Loans | ICollection&lt;Loan&gt; | ניווט |

### Computer (המשאב המוגבל)
| שדה | טיפוס | הערות |
|---|---|---|
| Id | int | PK |
| AssetTag | string | חובה, ייחודי (למשל "LAP-001") |
| Name | string | חובה, דגם |
| Specs | string? | מפרט חופשי |
| Status | ComputerStatus | |
| **Version** | int | `[ConcurrencyCheck]`, עולה ב-1 בכל עדכון (דריסת `SaveChangesAsync` ב-DbContext) |
| CreatedAtUtc | DateTime | |
| Loans | ICollection&lt;Loan&gt; | ניווט |
| Tags | ICollection&lt;Tag&gt; | many-to-many |

### Loan
| שדה | טיפוס | הערות |
|---|---|---|
| Id | int | PK |
| UserId | int | FK → User |
| ComputerId | int | FK → Computer |
| LoanedAtUtc | DateTime | |
| DueAtUtc | DateTime | |
| ReturnedAtUtc | DateTime? | |
| Status | LoanStatus | |
| User, Computer | ניווטים | |

### Tag
| שדה | טיפוס | הערות |
|---|---|---|
| Id | int | PK |
| Name | string | חובה, ייחודי |
| Computers | ICollection&lt;Computer&gt; | many-to-many |

### קשרים (Fluent API)
- `User 1→N Loan` ו-`Computer 1→N Loan`, עם `DeleteBehavior.Restrict`.
- `Computer N↔N Tag` דרך `UsingEntity` (טבלת `ComputerTag`).
- ב-Seed: Admin אחד (`admin@demo.com`), Borrower אחד (`user@demo.com`), כ-8 מחשבים `Available` ו-4 תגיות מקושרות.

---

## DTOs

| קבוצה | DTOs |
|---|---|
| Auth | `RegisterRequest` (FullName, Email, Password), `LoginRequest` (Email, Password), `AuthResponse` (Token, ExpiresAtUtc, Email, Role) |
| Computers | `ComputerResponse` (Id, AssetTag, Name, Specs, Status, Tags), `CreateComputerRequest` (AssetTag, Name, Specs), `UpdateComputerRequest` (Name, Specs, Status), `SetComputerTagsRequest` (TagIds) |
| Loans | `CreateLoanRequest` (ComputerId, DueAtUtc), `UpdateLoanRequest` (Status), `LoanResponse` (Id, ComputerId, ComputerName, AssetTag, UserId, UserName, LoanedAtUtc, DueAtUtc, ReturnedAtUtc, Status) |
| Tags | `TagResponse` (Id, Name), `CreateTagRequest` (Name) |
| כללי | `PagedResult<T>` (Items, TotalCount, Page, PageSize), `ComputerQuery`, `LoanQuery`, `ErrorResponse` (Status, Message, CorrelationId) |

## תרגום Result לסטטוס HTTP

`Ok → 200` · `Created → 201 + Location` · `NoContent → 204` · `Validation → 400` · `Forbidden → 403` · `NotFound → 404` · `Conflict → 409`.
בלי token או עם token לא תקין → 401 (מטפל ה-JWT middleware).

---

## Controllers ו-API

### AuthController — `/api/auth`
| Method | נתיב | גוף | הרשאה | סטטוסים |
|---|---|---|---|---|
| POST | `/register` | RegisterRequest | פתוח | 201, 400, 409 (אימייל קיים) |
| POST | `/login` | LoginRequest | פתוח | 200 (AuthResponse), 400, 401 |

### ComputersController — `/api/computers`
| Method | נתיב | גוף / שאילתה | הרשאה | סטטוסים |
|---|---|---|---|---|
| GET | `/` | `?status=&tagId=&search=&sortBy=&desc=&page=&pageSize=` | מחוברת | 200 (PagedResult) |
| GET | `/{id}` | | מחוברת | 200, 404 |
| POST | `/` | CreateComputerRequest | Admin | 201 + Location, 400, 403, 409 |
| PUT | `/{id}` | UpdateComputerRequest | Admin | 204, 400, 403, 404, 409 |
| DELETE | `/{id}` | | Admin | 204, 403, 404, 409 (יש הלוואה פעילה) |
| PUT | `/{id}/tags` | SetComputerTagsRequest | Admin | 204, 400, 403, 404 |

### LoansController — `/api/loans`
| Method | נתיב | גוף / שאילתה | הרשאה | סטטוסים |
|---|---|---|---|---|
| POST | `/` | CreateLoanRequest | מחוברת | 201 + Location, 400, 404, **409** |
| GET | `/` | `?status=&computerId=&userId=&page=&pageSize=` | מחוברת. Borrower רואה רק את ההלוואות שלה, Admin רואה הכול | 200 (PagedResult) |
| GET | `/{id}` | | בעלת ההלוואה או Admin | 200, 403, 404 |
| PATCH | `/{id}` | UpdateLoanRequest (`Status = Returned`) | בעלת ההלוואה או Admin | 200, 400, 403, 404, 409 |

ההחזרה היא PATCH ולא `/return`, כי הדרישות אוסרות פעולות כשמות ב-URL.

### TagsController — `/api/tags`
| Method | נתיב | גוף | הרשאה | סטטוסים |
|---|---|---|---|---|
| GET | `/` | | מחוברת | 200 |
| POST | `/` | CreateTagRequest | Admin | 201 + Location, 400, 403, 409 |
| DELETE | `/{id}` | | Admin | 204, 403, 404 |

---

## חוקים עסקיים (ב-Service)

**הרשמה:** תמיד `Role = Borrower`. Admin נוצר רק ב-Seed. אימייל כפול → 409.

**השאלה (`LoanService.BorrowAsync`), בטרנזקציה אחת:**
1. `DueAtUtc` בעתיד, לא יותר מ-`MaxLoanDays` (ברירת מחדל 14), ו-`Kind = Utc`.
2. למשתמשת פחות מ-`MaxActiveLoansPerUser` הלוואות פעילות (ברירת מחדל 2).
3. המחשב קיים (אחרת 404) ו-`Status == Available` (אחרת 409 "המחשב כבר מושאל").
4. `computer.Status = Loaned`, יצירת `Loan` עם `Status = Active`, ושמירה (ה-`Version` עולה אוטומטית).
5. `ConcurrencyConflictException` (שנזרקת מה-Repository על `DbUpdateConcurrencyException`) → `LogWarning` + 409 "המחשב נתפס על ידי משתמש אחר. נסי שוב."

**החזרה (`ReturnAsync`):** רק בעלת ההלוואה או Admin (אחרת 403). רק הלוואה `Active` (אחרת 409). מעדכנים `ReturnedAtUtc`, `Status = Returned` ו-`computer.Status = Available`, באותה טרנזקציה ועם אותו טיפול בחריגת התחרות.

**מחשבים:** `AssetTag` כפול → 409. מחשב עם הלוואה פעילה לא נמחק ולא עובר ל-`InMaintenance` (409). לא ניתן להגדיר `Status = Loaned` דרך ה-PUT, הסטטוס הזה משתנה רק דרך הלוואה.

**הרשאות בפועל:** Admin מנהל מחשבים ותגיות ורואה את כל ההלוואות. Borrower מלווה ומחזירה ורואה רק את ההלוואות שלה.

## Configuration

```json
{
  "ConnectionStrings": { "Default": "Host=localhost;Port=5432;Database=ComputerLendingDb;Username=postgres;Password=..." },
  "Jwt": { "Key": "...", "Issuer": "ComputerLending", "Audience": "ComputerLendingClient", "ExpiresMinutes": 60 },
  "LoanRules": { "MaxActiveLoansPerUser": 2, "MaxLoanDays": 14 }
}
```
`ConnectionStrings:Default` ו-`Jwt:Key` נשמרים ב-`appsettings.json`.
