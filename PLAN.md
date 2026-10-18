# PLAN – מערכת השאלת מחשבים (ASP.NET Core Web API)

איך עובדים עם הקובץ: עוברים שלב אחר שלב, בסדר. מסמנים `[x]` רק אחרי שעמדתם ב-**"סיימנו כש..."** של השלב.
בסוף כל שלב עושים commit. אחרי כל שלב ב-API בודקים ב-Swagger לפני שממשיכים.

---

## החלטות שננעלות עכשיו

| נושא | החלטה |
|---|---|
| המשאב המוגבל | `Computer` (מחשב פיזי אחד) |
| מנגנון התחרות | Optimistic concurrency: עמודת `Version` (מספר שעולה ב-1 בכל עדכון) על `Computer`, עם `[ConcurrencyCheck]` |
| מסד נתונים | PostgreSQL: מקומית דרך Docker, וב-Render בענן. אותו DB בפיתוח ובייצור, כך שה-migrations נשארות אותן migrations |
| תאריכים | `DateTime.UtcNow` בלבד, בכל המערכת. Npgsql ממפה ל-`timestamptz` וזורק חריגה על `DateTime.Now` ועל תאריך שה-Kind שלו אינו Utc |
| Roles | `Admin`, `Borrower` |
| ישויות | `User`, `Computer`, `Loan`, `Tag` (+ טבלת חיבור `ComputerTag`) |
| קשרים | `User 1→N Loan`, `Computer 1→N Loan`, `Computer N↔N Tag` |

למה ה-`Version` יושב על `Computer` ולא על `Loan`: ההתחרות היא על המחשב. שורת `Loan` חדשה לא מתנגשת עם אף אחד, ולכן אין בה מה לבדוק.
בשני בקשות במקביל שתיהן קוראות את המחשב עם `Version = 5`. הראשונה שומרת ומעדכנת ל-6. השנייה מנסה `UPDATE ... WHERE Id=1 AND Version=5`, לא מוצאת שורה, ו-EF זורק `DbUpdateConcurrencyException`.

---

## שלב 0 – הכנות (חצי שעה)

- [ ] התקנה: .NET SDK, Git, Node.js (ללקוח), Docker Desktop, חשבון GitHub, חשבון Render.
- [ ] `dotnet tool install --global dotnet-ef`
- [ ] ב-Cursor: תוסף C# (או C# Dev Kit אם זמין), ותוכנה לצפייה ב-PostgreSQL (למשל pgAdmin או DBeaver).
- [ ] מרימות PostgreSQL מקומי בלי התקנה (בפעמים הבאות מפעילות אותו עם `docker start pg`):

```bash
docker run --name pg -e POSTGRES_PASSWORD=<בחרי-סיסמה> -p 5432:5432 -d postgres:16
```
- [ ] יוצרים `.cursor/rules/architecture.md` ושמים בו את הכללים, כדי ש-AI של Cursor לא ישבור את הארכיטקטורה:
  - Core לא מפנה לשום פרויקט או חבילה חיצונית (בלי EF Core).
  - Controller לא מזריק DbContext או Repository, רק Service.
  - כל הקוד אסינכרוני, `CancellationToken` עובר לכל אורך השרשרת, בלי `.Result` ובלי `.Wait()`.
  - ישויות לא יוצאות מה-API, רק DTOs.
  - בלי סודות בקוד.
  - תאריכים ב-UTC.
  - ולידציה עסקית ב-Service ולא ב-Controller.
- [ ] שומרים בתיקיית הפרויקט את קובץ הדרישות (docx) ואת הקובץ הזה, כדי שאפשר יהיה להפנות אליהם ב-Cursor.

**סיימנו כש...** הכלים מותקנים ויש כללי Cursor בתיקייה.

---

## שלב 1 – Solution ופרויקטים

```bash
mkdir ComputerLending && cd ComputerLending
git init
dotnet new sln -n ComputerLending
dotnet new classlib -n ComputerLending.Core
dotnet new classlib -n ComputerLending.Data
dotnet new classlib -n ComputerLending.Service
dotnet new webapi  -n ComputerLending.API --use-controllers
dotnet new xunit   -n ComputerLending.Tests
dotnet sln add ComputerLending.Core ComputerLending.Data ComputerLending.Service ComputerLending.API ComputerLending.Tests

dotnet add ComputerLending.Data    reference ComputerLending.Core
dotnet add ComputerLending.Service reference ComputerLending.Core
dotnet add ComputerLending.API     reference ComputerLending.Core ComputerLending.Service ComputerLending.Data
dotnet add ComputerLending.Tests   reference ComputerLending.Core ComputerLending.Service ComputerLending.Data
```

- [x] מוחקים את `Class1.cs` ואת `WeatherForecast`.
- [x] יוצרים `.gitignore` בשורש: `dotnet new gitignore`. מוסיפים לו גם `appsettings.Development.json`, `logs/` ו-`secrets*`.
- [ ] commit ראשון.

**סיימנו כש...** `dotnet build` עובר והפניות הפרויקטים נכונות. Core לא מפנה לכלום.

---

## שלב 2 – שכבת Core (בלי חבילות חיצוניות)

- [x] **Enums:** `UserRole { Admin, Borrower }`, `ComputerStatus { Available, Loaned, InMaintenance }`, `LoanStatus { Active, Returned }`.
- [x] **Entities:**
  - `User`: Id, FullName, Email, PasswordHash, Role, CreatedAtUtc, `ICollection<Loan> Loans`.
  - `Computer`: Id, AssetTag, Name, Specs, Status, **`[ConcurrencyCheck] int Version`**, CreatedAtUtc, `Loans`, `Tags`.
  - `Loan`: Id, UserId, ComputerId, LoanedAtUtc, DueAtUtc, ReturnedAtUtc?, Status, ניווטים ל-`User` ול-`Computer`.
  - `Tag`: Id, Name, `Computers`.
- [x] **Interfaces** (`IUserRepository`, `IComputerRepository`, `ILoanRepository`, `ITagRepository`, `IUnitOfWork`):
  - `IUnitOfWork` כולל `SaveChangesAsync(ct)` ו-`BeginTransactionAsync(ct)`. המתודה האחרונה מחזירה `ITransactionScope : IAsyncDisposable` עם `CommitAsync`, מוגדר ב-Core.
  - ממשקי השירותים: `IAuthService`, `IComputerService`, `ILoanService`, `ITagService`, `ITokenService`.
- [x] **DTOs** (Request נפרד מ-Response):
  - Auth: `RegisterRequest`, `LoginRequest`, `AuthResponse`.
  - Computers: `ComputerResponse`, `CreateComputerRequest`, `UpdateComputerRequest`, `SetComputerTagsRequest`.
  - Loans: `CreateLoanRequest`, `UpdateLoanRequest`, `LoanResponse`.
  - Tags: `TagResponse`, `CreateTagRequest`.
  - כלליים: `PagedResult<T>`, `ComputerQuery` / `LoanQuery` (פרמטרי סינון, מיון ו-paging).
  - ולידציה בעזרת Data Annotations (`[Required]`, `[EmailAddress]`, `[StringLength]`, `[Range]`).
- [x] **Result pattern:** `Result<T>` עם `ResultStatus { Ok, Created, NotFound, Forbidden, Conflict, Validation }` והודעה. ה-Controller יתרגם כל סטטוס ל-HTTP.
- [x] **חריגה משלנו:** `ConcurrencyConflictException` ב-Core. ה-Repository יזרוק אותה, ה-Service יתפוס אותה.
- [x] **Options classes:** `JwtOptions` (Key, Issuer, Audience, ExpiresMinutes) ו-`LoanRulesOptions` (MaxActiveLoansPerUser, MaxLoanDays).

**סיימנו כש...** Core מתקמפל, וב-`.csproj` שלו אין אף `PackageReference`.

---

## שלב 3 – שכבת Data (DbContext, Fluent API, Repositories)

```bash
dotnet add ComputerLending.Data package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add ComputerLending.API  package Microsoft.EntityFrameworkCore.Design
```

- [x] `AppDbContext` עם `DbSet` לכל ישות.
- [x] **Fluent API** (`IEntityTypeConfiguration<T>` לכל ישות):
  - אינדקס ייחודי על `User.Email`, `Computer.AssetTag` ו-`Tag.Name`.
  - אורכי שדות וערכי חובה.
  - שמירת enums כ-string: `.HasConversion<string>()`.
  - `Computer.Version` מסומן `.IsConcurrencyToken()`.
  - קשר many-to-many עם `UsingEntity` לטבלת `ComputerTag`.
  - `OnDelete(DeleteBehavior.Restrict)` ל-Loan כדי שלא ימחקו הלוואות בטעות.
  - בלי הגדרות שתלויות ב-provider (כמו `HasColumnType("timestamptz")`), כדי שהבדיקה של שלב 10 תוכל לרוץ גם על SQLite.
  - לא כותבים SQL ידני: שמות טבלאות ועמודות ב-PostgreSQL רגישים לאותיות גדולות וקטנות.
- [x] **דריסת `SaveChangesAsync(bool, CancellationToken)`** כדי שה-Version יעלה אוטומטית ואף service לא יצטרך לזכור:

```csharp
public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
{
    foreach (var entry in ChangeTracker.Entries<Computer>().Where(e => e.State == EntityState.Modified))
        entry.Entity.Version++;   // הערך המקורי נשאר ב-OriginalValues, ומשם נבנה ה-WHERE

    return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
}
```

- [x] **Repositories:** מימוש כל הממשקים:
  - שאילתות קריאה עם `AsNoTracking()`.
  - Skip/Take בתוך השאילתה ו-`CountAsync` נפרד.
  - `Include` / `ThenInclude` ב-`Loan → Computer → Tags`.
  - בלי שאילתות בלולאה.
- [x] **ה-UnitOfWork** תופס את החריגות ומתרגם אותן:

```csharp
try { await _db.SaveChangesAsync(ct); }
catch (DbUpdateConcurrencyException ex)
{
    throw new ConcurrencyConflictException("המחשב נתפס על ידי משתמש אחר", ex);
}
catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
{
    // שתי בקשות יצרו בו-זמנית אימייל או AssetTag זהים, ואף אחת לא ראתה את השנייה בבדיקה המוקדמת
    throw new ConcurrencyConflictException("הערך כבר קיים", ex);
}
```

- [ ] ב-PostgreSQL אין נעילה של כל הקובץ כמו ב-SQLite, ולכן שתי הלוואות באמת יכולות להגיע במקביל. זה בדיוק מה ש-`Version` בא לתפוס.

**סיימנו כש...** הכול מתקמפל. עדיין אין DB, והוא יווצר בשלב הבא.

---

## שלב 4 – Configuration, Migrations ו-Seed (חובה שתי migrations)

- [x] ב-`appsettings.json` (כולל ה-connection string):

```json
{
  "ConnectionStrings": { "Default": "Host=localhost;Port=5432;Database=ComputerLendingDb;Username=postgres;Password=..." },
  "Jwt": { "Key": "...", "Issuer": "ComputerLending", "Audience": "ComputerLendingClient", "ExpiresMinutes": 60 },
  "LoanRules": { "MaxActiveLoansPerUser": 2, "MaxLoanDays": 14 }
}
```

- [x] ה-connection string המלא (`ConnectionStrings:Default`) ומפתח ה-JWT (`Jwt:Key`) נשמרים ב-`appsettings.json`, כדי שהפרויקט ירוץ בלי User Secrets.

- [x] ב-`Program.cs`: `AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")))`, ורישום ה-Repositories וה-UnitOfWork.
- [x] **Migration ראשונה** רק עם `User`, `Computer` ו-`Loan` (לפני שמוסיפים `Tag`):

```bash
dotnet ef migrations add Init -p ComputerLending.Data -s ComputerLending.API
dotnet ef database update   -p ComputerLending.Data -s ComputerLending.API
```

- [x] מוסיפים `Tag` + `ComputerTag` ל-DbContext, ואז **migration שנייה**: `AddTags`, ואחריה `database update`.
- [x] בעליית המערכת רצה `await db.Database.MigrateAsync()` ואחריה ה-Seed, כדי שהמערכת תעלה מ-DB ריק בלי פעולה ידנית (גם ב-Render).
- [x] **Seed** (בקוד של `DbSeeder` שרץ אם הטבלאות ריקות, כדי לייצר hash אמיתי של סיסמה עם BCrypt ותאריכים עם `DateTime.UtcNow`):
  - Admin אחד (`admin@demo.com`) ו-Borrower אחד (`user@demo.com`).
  - כ-8 מחשבים עם `Status = Available`.
  - 4 תגיות וקישורים ביניהם.
- [x] פותחים את הטבלאות ב-pgAdmin או ב-DBeaver ובודקים שהכול נכנס.

**סיימנו כש...** מחיקת בסיס הנתונים (`dotnet ef database drop -p ComputerLending.Data -s ComputerLending.API`) והרצה מחדש של השרת יוצרות הכול לבד, בלי שום פעולה ידנית.

---

## שלב 5 – Middleware, לוגים ו-Swagger (תשתית לפני ה-Controllers)

```bash
dotnet add ComputerLending.API package NLog.Web.AspNetCore
dotnet add ComputerLending.API package Swashbuckle.AspNetCore
dotnet add ComputerLending.API package Microsoft.AspNetCore.Authentication.JwtBearer
```

- [x] `CorrelationIdMiddleware`: לוקח `X-Correlation-Id` אם הגיע או יוצר Guid, מחזיר אותו ב-response header, ו-`using (_logger.BeginScope(new Dictionary<string, object>{ ["CorrelationId"] = id }))`.
- [x] `ExceptionHandlingMiddleware`: תופס כל חריגה, כותב `LogError`, ומחזיר JSON אחיד `{ status, message, correlationId }`. בלי stack trace ללקוח.
- [x] `nlog.config`: קובץ לוגים בתיקיית `logs/` (נכנס ל-`.gitignore`), ו-layout שכולל `${scopeproperty:CorrelationId}`.
- [x] ב-`Program.cs` מחליפים את הלוגר: `builder.Host.UseNLog()`.
- [x] **סדר ה-pipeline:**
  1. `ExceptionHandlingMiddleware`
  2. `CorrelationIdMiddleware`
  3. `UseSwagger` + `UseSwaggerUI`
  4. `UseCors`
  5. `UseAuthentication`
  6. `UseAuthorization`
  7. `MapControllers`
- [x] Swagger עם הגדרת Bearer, כדי שאפשר יהיה ללחוץ Authorize ולהדביק token.
- [x] CORS פתוח לכתובת הלקוח (`http://localhost:5173` בפיתוח).
- [x] **לא מתעדים** סיסמאות, tokens או גוף בקשת הרשמה. בלוג של בקשה כותבים רק method, path, status ו-CorrelationId.

**סיימנו כש...** בקשה לכל נתיב לא קיים מחזירה JSON אחיד, וקובץ הלוג מכיל את ה-CorrelationId.

---

## שלב 6 – Auth (הרשמה, התחברות, JWT)

```bash
dotnet add ComputerLending.Service package BCrypt.Net-Next
dotnet add ComputerLending.Service package Microsoft.IdentityModel.Tokens
dotnet add ComputerLending.Service package System.IdentityModel.Tokens.Jwt
```

- [x] `TokenService` (מימוש `ITokenService`): claims של `NameIdentifier`, `Email` ו-`Role`, חתימה עם המפתח מה-secrets.
- [x] `AuthService`:
  - `Register`: בודק אימייל קיים → Conflict, מבצע hash, ותמיד `Role = Borrower`.
  - `Login`: מוצא את המשתמשת ובודק את הסיסמה. הודעה כללית "אימייל או סיסמה שגויים" גם כשהאימייל לא קיים.
- [x] `AuthController` עם `POST /api/auth/register` ו-`POST /api/auth/login`.
- [x] ב-`Program.cs`: `AddAuthentication().AddJwtBearer(...)` עם אימות Issuer, Audience, Lifetime ו-SigningKey.
- [x] בדיקה ב-Swagger: login כ-Admin, העתקת ה-token, ולחיצה על Authorize.

**סיימנו כש...** login מחזיר token, ובקשה ל-endpoint מוגן בלי token מחזירה 401.

---

## שלב 7 – Tags (ה-Controller הפשוט, לחימום)

- [x] `TagService` + `TagsController`: `GET /api/tags`, `POST /api/tags` (Admin), `DELETE /api/tags/{id}` (Admin).
- [x] AutoMapper: `MappingProfile` (Entity ↔ DTO). מומלץ להתקין את הגרסה שנלמדה בקורס. לפי מה שאני יודעת, גרסאות חדשות מאוד דורשות רישיון (יש רישיון חינמי לקהילה), כדאי לבדוק.
- [x] סטטוסים: 201 עם Location, 204, 404, 409 (תגית כפולה), 403 (למי שאינה Admin).

**סיימנו כש...** Borrower מקבל 403 על POST, ו-Admin מצליח.

---

## שלב 8 – Computers

- [x] `ComputerService` + `ComputersController` לפי הטבלה:

| Method | נתיב | הרשאה |
|---|---|---|
| GET | `/api/computers?status=&tagId=&search=&sortBy=&desc=&page=&pageSize=` | מחוברת |
| GET | `/api/computers/{id}` | מחוברת |
| POST | `/api/computers` | Admin |
| PUT | `/api/computers/{id}` | Admin |
| DELETE | `/api/computers/{id}` | Admin |
| PUT | `/api/computers/{id}/tags` | Admin |

- [x] שימוש נכון ב-`[FromRoute]`, `[FromQuery]` ו-`[FromBody]`.
- [x] pagination אמיתי: `Skip`/`Take` בשאילתה, ו-`PagedResult` מחזיר `Items`, `TotalCount`, `Page`, `PageSize`.
- [x] ולידציה עסקית ב-Service:
  - `AssetTag` כפול → 409.
  - מחשב עם הלוואה פעילה לא נמחק (409) ולא עובר לתחזוקה.
  - אי אפשר להגדיר `Status = Loaned` ידנית דרך ה-PUT. הסטטוס הזה משתנה רק דרך הלוואה.

**סיימנו כש...** Swagger מציג את כל ה-endpoints, והסינון והמיון עובדים. סימון SQL ב-log של EF מראה `LIMIT/OFFSET`.

---

## שלב 9 – Loans והתחרות על המשאב (הלב של הפרויקט)

- [x] `LoanService.BorrowAsync` (בסדר הזה):
  1. ולידציה: `DueAtUtc` עתידי ולא יותר מ-`MaxLoanDays` (מ-`IOptions<LoanRulesOptions>`). מוודאים גם שה-`Kind` הוא `Utc`: תאריך שהגיע מה-JSON בלי `Z` יוצא `Unspecified`, ו-Npgsql יזרוק חריגה בשמירה.
  2. המשתמשת קיימת, וסך ההלוואות הפעילות שלה קטן מ-`MaxActiveLoansPerUser`.
  3. פותחים טרנזקציה (`BeginTransactionAsync`).
  4. קוראים את המחשב עם tracking. לא קיים → NotFound. `Status != Available` → Conflict ("המחשב כבר מושאל").
  5. `computer.Status = Loaned`, יוצרים `Loan`, שומרים (ה-Version עולה אוטומטית).
  6. `CommitAsync`.
  7. `catch (ConcurrencyConflictException)` → `LogWarning` עם `ComputerId`, `UserId` ו-CorrelationId, ומחזירים `Result.Conflict("המחשב נתפס על ידי משתמש אחר. נסי שוב.")`.
- [x] `LoanService.ReturnAsync` (מופעל מ-PATCH): רק בעלת ההלוואה או Admin (אחרת Forbidden). הלוואה שכבר הוחזרה → Conflict. מעדכנים `ReturnedAtUtc`, `Status = Returned`, ו-`computer.Status = Available`, באותה טרנזקציה ועם אותו טיפול בחריגה.
- [x] `LoansController`:

| Method | נתיב | הרשאה | סטטוסים |
|---|---|---|---|
| POST | `/api/loans` | מחוברת | 201 + Location, 400, 404, 409 |
| GET | `/api/loans?status=&computerId=&userId=&page=&pageSize=` | Borrower רואה רק את שלה, Admin את הכול | 200 |
| GET | `/api/loans/{id}` | בעלת ההלוואה או Admin | 200, 403, 404 |
| PATCH | `/api/loans/{id}` (`status: Returned`) | בעלת ההלוואה או Admin | 200, 400, 403, 404, 409 |

  (ההחזרה היא PATCH ולא `/return`, כי הדרישות אוסרות פעולות כשמות ב-URL.)
- [x] ה-Controller מתרגם `Result` ל-HTTP, והתרגום מרוכז במקום אחד (extension method) כדי לא לחזור עליו.
- [x] בודקים ידנית: שני טאבים של Swagger לוחצים POST על אותו מחשב בהפרש של שנייה. אחד מקבל 201, השני 409.

**סיימנו כש...** אותו מחשב לא יכול להיות על שתי הלוואות פעילות, וב-log מופיע Warning על ההתנגשות.

---

## שלב 10 – בדיקות (xUnit + Moq)

```bash
dotnet add ComputerLending.Tests package Moq
dotnet add ComputerLending.Tests package Microsoft.EntityFrameworkCore.Sqlite
```

- [x] **בדיקות Service עם Moq** (`LoanServiceTests`):
  - הלוואה תקינה → Created, וסטטוס המחשב `Loaned`.
  - מחשב לא פנוי → Conflict, ולא נקרא `SaveChangesAsync`.
  - חריגת מכסה → Validation.
  - ה-UnitOfWork המדומה זורק `ConcurrencyConflictException` → `Result.Conflict`.
  - החזרה: מי שאינה הבעלים → Forbidden.
- [x] **בדיקת ההתנגשות האמיתית** (נבדקת בנפרד, לא להחליף ב-InMemory של EF, כי הוא לא אוכף concurrency tokens). היא רצה על SQLite in-memory, רק בפרויקט הבדיקות, כדי שתעבור על כל מחשב בלי Docker. המנגנון זהה, כי `Version` היא עמודה רגילה. (אפשרות מתקדמת: `Testcontainers.PostgreSql` להרצה על Postgres אמיתי.)

```csharp
using var conn = new SqliteConnection("DataSource=:memory:");
conn.Open();
var opts = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options;

using (var setup = new AppDbContext(opts)) { setup.Database.EnsureCreated(); /* מוסיפים מחשב אחד Available */ }

using var ctx1 = new AppDbContext(opts);
using var ctx2 = new AppDbContext(opts);
var c1 = await ctx1.Computers.FirstAsync();
var c2 = await ctx2.Computers.FirstAsync();

c1.Status = ComputerStatus.Loaned;
c2.Status = ComputerStatus.Loaned;

await ctx1.SaveChangesAsync();                                              // מצליח
await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctx2.SaveChangesAsync()); // נכשל
```

- [x] `dotnet test` ירוק.

**סיימנו כש...** כל הבדיקות עוברות. בדיקת שני ה-DbContext נכשלת אם מוחקים את `[ConcurrencyCheck]` (מוודאים פעם אחת כדי להיות בטוחות שהיא אכן בודקת משהו).

---

## שלב 11 – ניקוי איכות לפני הלקוח

- [x] עוברים על רשימת הדרישות של המסמך (חלק ב', סעיפים 1–13) ומסמנים לכל סעיף איפה הוא מתקיים.
  1. REST: `Controllers/*`. סטטוסים ב-`ResultHttpExtensions` (201 עם Location). Swagger ב-`Program.cs`. ההחזרה היא `PATCH /api/loans/{id}`, בלי שם פעולה ב-URL.
  2. Binding: `[FromRoute]` / `[FromQuery]` / `[FromBody]` בקונטרולרים. Data Annotations על ה-DTOs, ו-`[ApiController]` מחזיר 400 על ModelState. ולידציה עסקית ב-Service בלבד.
  3. ארכיטקטורה: ארבעה פרויקטים. `Core` בלי חבילות. `Data` מופיע רק ב-`Program.cs` (רישום ו-seed), לא בקונטרולר.
  4. DI: הזרקה ב-constructor, interfaces ב-Core, `AddScoped` ב-`Program.cs`. `AddDbContext` הוא scoped.
  5. EF: `AppDbContext` עם DbSet לכל ישות. מיגרציות `Init` ו-`AddTags`. connection string מהקונפיגורציה. `DbSeeder` בעלייה.
  6. CRUD וסינון ב-`ComputerRepository` / `LoanRepository`, `Skip`/`Take` בתוך השאילתה, `AsNoTracking` בקריאה.
  7. קשרים: User–Loan (one-to-many), Computer–Tag (many-to-many, `ComputerTag`). `Include`/`ThenInclude` ו-Fluent API ב-`Configurations`.
  8. ישויות לא יוצאות מה-API. Request ו-Response נפרדים. `MappingProfile`.
  9. async עם `CancellationToken` לכל האורך. אין `.Result` ואין `.Wait()`.
  10. `ExceptionHandlingMiddleware` ואז `CorrelationIdMiddleware`. `UseAuthentication` לפני `UseAuthorization`.
  11. `appsettings.json` + `appsettings.Development.json`. `IOptions<JwtOptions>` ו-`IOptions<LoanRulesOptions>`. ה-connection string ומפתח ה-JWT ב-`appsettings.json`.
  12. `AuthService` + `TokenService`. roles Admin ו-Borrower. `[Authorize]` על הקונטרולרים, `[Authorize(Roles = "Admin")]` על כתיבה.
  13. `LoanServiceTests` (Moq) ו-`ConcurrencyTests` (שני DbContext על SQLite).
- [x] בדיקה שאין `.Result` / `.Wait()` / `new SomeService()`. ה-`new LoanService` היחיד הוא ב-`ConcurrencyTests`, שם הוא נבנה עם המימושים האמיתיים של ה-Data.
- [x] בדיקה שכל קריאה-בלבד משתמשת ב-`AsNoTracking()`. תוקן `DbSeeder`. `GetByIdsAsync` נשאר במעקב בכוונה: התגיות מתווספות למחשב שכבר טעון באותו context.
- [x] בדיקה שאין שאילתה בלולאה (N+1). בלוג של EF, `GET /api/computers` הוא COUNT + `LIMIT`/`OFFSET` + JOIN אחד לתגיות של העמוד. `GET /api/loans` הוא COUNT + עמוד עם JOIN + שאילתת פיצול אחת לתגיות.
- [x] `appsettings.Development.json` ו-`logs/` ב-`.gitignore`. ה-connection string ו-`Jwt:Key` נמצאים ב-`appsettings.json` בכוונה, כדי שההגשה תרוץ בלי User Secrets. סיסמאות הדמו נמצאות ב-`DbSeeder`.

---

## שלב 12 – לקוח React (המינימום)

```bash
npm create vite@latest computer-lending-client -- --template react
```

ארבעה מסכים בלבד:
- [x] התחברות והרשמה (שמירת ה-JWT ושליחתו ב-`Authorization: Bearer`).
- [x] רשימת מחשבים עם pagination אמיתי מול השרת (`page` ו-`pageSize`).
- [x] כפתור "השאילי" על מחשב.
- [x] הודעה ברורה כשהשרת מחזיר **409** ("המחשב נתפס על ידי משתמש אחר").
- [x] כתובת השרת במשתנה סביבה (`VITE_API_URL`), בלי כתובת קבועה בקוד.
- [x] תאריך ההחזרה נשלח בפורמט `new Date(...).toISOString()` (עם `Z`).

**סיימנו כש...** שני דפדפנים (אחד רגיל ואחד פרטי) לוחצים "השאילי" על אותו מחשב, ואחד מהם רואה את הודעת ה-409.

---

## שלב 13 – README ו-GitHub

- [x] `README.md` בשרת:
  - תיאור המערכת בפסקה.
  - פסקה על המשאב המוגבל: המחשב, איפה התחרות (`BorrowAsync`), ואיך המערכת מגיבה (עמודת `Version`, חריגה, 409, לוג Warning).
  - הוראות הרצה מקומית: הרצת PostgreSQL ב-Docker, connection string ו-`Jwt:Key` ב-`appsettings.json`, ו-`dotnet run`. ה-migrations וה-Seed רצים לבד בעליית השרת.
  - משתמשי דמו עם פרטי כניסה לכל role.
- [x] שני repositories ב-GitHub: שרת ולקוח, בלי `bin`, `obj`, `node_modules`, `*.db`, `logs`.
- [x] בדיקת "מחשב נקי": clone לתיקייה חדשה, והרצה לפי ה-README בלבד.

---

## שלב 14 – העלאה ל-Render

- [ ] **בסיס נתונים:** ב-Render יוצרים PostgreSQL (New → PostgreSQL), באותו Region של השרת. משתמשים ב-Internal Database URL.
- [ ] **connection string:** Render נותן כתובת בפורמט `postgres://user:pass@host:5432/db`, ו-Npgsql לא מקבל אותה כמו שהיא. כותבים אותה ידנית בפורמט `Host=...;Port=5432;Database=...;Username=...;Password=...` במשתנה הסביבה `ConnectionStrings__Default`. אם משתמשים ב-External URL מוסיפים `SSL Mode=Require;Trust Server Certificate=true`.
- [ ] **שרת:** Web Service מסוג Docker (`Dockerfile` ל-.NET). משתני סביבה: `ConnectionStrings__Default`, `Jwt__Key`, `ASPNETCORE_ENVIRONMENT=Production`. כדאי לוודא שהפורט ש-Render מצפה לו תואם לפורט של הקונטיינר (ב-.NET 8 ומעלה ברירת המחדל היא 8080, אפשר להגדיר `PORT=8080`).
- [ ] ה-migrations וה-Seed רצים לבד בעליית השרת (שלב 4), אין צורך להריץ ידנית.
- [ ] **לקוח:** Static Site, עם `VITE_API_URL` שמצביע על כתובת השרת ב-Render.
- [ ] מעדכנים CORS בשרת לכתובת הלקוח ב-Render.
- [ ] בדיקה מקצה לקצה אחרי ההעלאה: הרשמה, login, רשימה, השאלה ו-409.
- [ ] כדאי לבדוק את תנאי המסלול החינמי של Render (למשל מגבלת זמן על בסיס הנתונים, והשרת "נרדם" ולכן הבקשה הראשונה איטית).

---

## בדיקה סופית לפני הגשה

- [ ] 4 פרויקטים בארכיטקטורה הנכונה (+ Tests), ו-Core בלי חבילות.
- [ ] PostgreSQL בלבד, שתי migrations לפחות, ו-Seed שרץ לבד מ-DB ריק.
- [ ] `[ConcurrencyCheck]` על `Computer.Version`, 409 עם הודעה, ובדיקת שני DbContext.
- [ ] NLog: בקשות, חריגות (Error), התנגשויות (Warning), ו-CorrelationId.
- [ ] JWT עם שני roles, והרשאות שונות בפועל.
- [ ] אין סודות ב-repo, ויש `.gitignore` תקין.
- [ ] README מלא ומשתמשי דמו.
- [ ] שני repositories, והעלאה ל-Render.
