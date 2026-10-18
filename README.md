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

## העלאה ל-Render

השרת עולה כ-Web Service מסוג Docker. ה-`Dockerfile` מפרסם את ה-API ומאזין על פורט 8080. אם Render מגדיר `PORT`, השרת מאזין עליו. המיגרציות וה-Seed רצים לבד בעלייה, מול מסד ריק.

1. ב-Render: New → PostgreSQL, באותו Region של השרת (ב-`render.yaml` זה Frankfurt). שומרים את ה-Internal Database URL.
2. New → Web Service, מחובר ל-repository של השרת, Runtime: Docker. אפשר גם New → Blueprint עם `render.yaml`, שיוצר את מסד הנתונים ואת השרת יחד.
3. משתני סביבה בשרת:
   - `ConnectionStrings__Default` — כתובת בפורמט `Host=...;Port=5432;Database=...;Username=...;Password=...`. כתובת `postgres://user:pass@host:5432/db` מתקבלת גם היא, והשרת ממיר אותה בעלייה. ל-Internal URL אין צורך ב-SSL. ל-External URL (מארח עם נקודה) השרת מוסיף `SSL Mode=Require`. אם כותבים את המחרוזת ידנית ל-External URL, מוסיפים גם `Trust Server Certificate=true`.
   - `Jwt__Key` — מחרוזת באורך 32 תווים לפחות. ב-Blueprint המפתח נוצר אוטומטית.
   - `ASPNETCORE_ENVIRONMENT=Production`
4. ה-CORS כולל את `http://localhost:5173` ואת `https://computer-lending-client.onrender.com`. אם כתובת האתר ב-Render שונה, מוסיפים `Cors__Origins__1` עם הכתובת המלאה, בלי סלאש בסוף.
5. בדיקת חיים: `GET /health` מחזיר 200. Swagger נמצא ב-`/swagger`.

במסלול החינמי מסד הנתונים מוגבל בזמן (למשל נמחק אחרי כ-30 יום), והשרת נרדם אחרי חוסר פעילות. הבקשה הראשונה אחרי שינה איטית, כי העלייה כוללת חיבור למסד, מיגרציה ו-Seed.

אחרי שהלקוח באוויר, בודקים הרשמה, login, רשימת מחשבים, השאלה, ו-409 כששני דפדפנים משאילים את אותו מחשב.
