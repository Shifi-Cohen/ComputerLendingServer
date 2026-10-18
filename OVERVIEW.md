# איך הפרויקט בנוי

מערכת השאלת מחשבים: שרת ASP.NET Core + PostgreSQL. הקליינט יבוא אחר כך ורק מדגים את השרת.

## בקשה (מי מדבר עם מי בזמן ריצה)

לקוח → Controller → Service → Repository → PostgreSQL → בחזרה כ-DTO, לא כישות.

ה-Controller **לא** ניגש ל-Data. הוא מקבל רק Service, קורא לו, ומתרגם `Result` ל-HTTP.

גם ה-Service לא מכיר את פרויקט Data: הוא מקבל ממשקים מ-Core (`IComputerRepository` וכו'), והמימושים ב-Data מחוברים ב-`Program.cs`.

## הפניות בין פרויקטים (קומפילציה)

```
API  →  Service  →  Core
Data →  Core
API  →  Data     (רק Program.cs: רישום DI, מיגרציות, Seed)
```

Core לא תלוי באף אחד. לכן אפשר להחליף PostgreSQL בלי לגעת בלוגיקה.

| פרויקט | אחראי על |
|---|---|
| **Core** | ישויות, Enums, DTOs, ממשקים, `Result`, חוקים כהגדרות. בלי EF, בלי חבילות. |
| **Data** | DbContext, Repositories, מיגרציות, Seed. מדבר עם PostgreSQL. |
| **Service** | לוגיקה עסקית (השאלה, הרשאות, ולידציה שתלויה בנתונים). |
| **API** | Controllers דקים + Middleware + `Program.cs`. מתרגם `Result` ל-HTTP. |
| **Tests** | בדיקות על ה-Service (Moq) ועל התחרות (שני DbContext). |

- צורת הקלט: Data Annotations על ה-DTO (חסר שדה, אימייל לא תקין).
- מצב הנתונים: ב-Service (המחשב כבר מושאל, מכסת הלוואות).
- הכל async, עם `CancellationToken`, בלי `.Result` / `.Wait()`.

## המשאב המוגבל

מחשב פיזי אחד (`Computer`). שתי משתמשות שלוחצות "השאילי" ביחד: רק אחת מקבלת 201, השנייה 409.

איך: שדה `Version` על המחשב. בכל שמירה הוא עולה. אם מישהי אחרת הספיקה לשנות את השורה, EF זורק חריגה → 409.

## עוד כללים קצרים

- תאריכים רק `DateTime.UtcNow` (Kind=Utc). אחרת Npgsql זורק.
- חיבור ל-DB, מפתח ה-JWT וסיסמאות ה-Seed ב-`appsettings.Development.json` (לא בגיט), או במשתני סביבה.
- שני תפקידים: Admin מנהל מלאי; Borrower מלווה ומחזירה.
- בעליית השרת רצות מיגרציות ו-Seed לבד. לפני הרצה: `docker start pg`.
- דמו: `admin@demo.com` ו-`user@demo.com`. הסיסמאות הן `Seed:AdminPassword` ו-`Seed:BorrowerPassword`.
