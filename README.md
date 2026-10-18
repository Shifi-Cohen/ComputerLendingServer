# השאלת מחשבים

מערכת לניהול השאלת מחשבים. השרת הוא ASP.NET Core Web API מעל PostgreSQL, מחולק ל-Core, Data, Service ו-API, עם פרויקט Tests. הלקוח הוא יישום React נפרד.

## המשאב המוגבל

המשאב המוגבל הוא המחשב: שתי בקשות לא יכולות להשאיל את אותו מחשב. התחרות נמצאת ב-`LoanService.BorrowAsync`. שם המחשב נטען, הסטטוס שלו משתנה ל-Loaned, ונשמרת הלוואה באותה טרנזקציה. לעמודת `Computer.Version` יש `[ConcurrencyCheck]`, והיא עולה בכל שמירה. אם בקשה אחרת הספיקה לשמור קודם, EF זורק `DbUpdateConcurrencyException`. ה-Repository הופך אותה ל-`ConcurrencyConflictException`, וה-Service כותב Warning ללוג ומחזיר 409 עם ההודעה "המחשב נתפס על ידי משתמש אחר. נסי שוב."

## הרצה מקומית

צריך Docker עם PostgreSQL. ה-connection string ו-`Jwt:Key` כבר נמצאים ב-`ComputerLending.API/appsettings.json`. המשתמש, הסיסמה והפורט של הקונטיינר צריכים להתאים ל-connection string.

```bash
docker run --name pg -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=REDACTED -p 5432:5432 -d postgres
```

אם הקונטיינר `pg` כבר קיים:

```bash
docker start pg
```

מתוך התיקייה הזו:

```bash
dotnet run --project ComputerLending.API --launch-profile http
```

המיגרציות וה-Seed רצים לבד בעליית השרת. השרת מאזין ב-http://localhost:5045. מסד הנתונים `ComputerLendingDb` נוצר בעלייה.

## משתמשי דמו

| תפקיד | אימייל | סיסמה |
| --- | --- | --- |
| Admin | admin@demo.com | Admin123! |
| Borrower | user@demo.com | User123! |

Admin נוצר רק ב-Seed. הרשמה דרך ה-API יוצרת Borrower.

## לקוח

הלקוח נמצא ב-repository נפרד. ב-`.env` שלו צריך `VITE_API_URL=http://localhost:5045`, ואז `npm install` ו-`npm run dev`. האתר נפתח ב-http://localhost:5173.
