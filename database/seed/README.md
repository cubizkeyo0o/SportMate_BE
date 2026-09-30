# Development seed data

`dev-court-discovery.sql` fills a **local development** database with courts for trying out court discovery in the app (Home → Courts → Court detail). It is not a migration, the API never runs it, and the automated tests do not use it (they build their own databases).

Never run it against a production database.

## What it creates

| Id | Court | Status | City | Sports (VND per hour) | Operating hours (court-local) |
|---|---|---|---|---|---|
| `5eed0000-0000-4000-8000-000000000001` | SportMate Tennis Center | Active | Ho Chi Minh City | Tennis 150,000; Pickleball 120,000 | Mon 06:00–12:00 **and** 16:00–22:00; Tue–Fri 06:00–22:00; Sat 07:00–23:00; Sun 07:00–22:00 |
| `5eed0000-0000-4000-8000-000000000002` | SportMate Badminton Arena | Active | Ho Chi Minh City | Badminton 100,000; Table Tennis 80,000; Basketball 180,000 | Mon–Fri 06:00–22:00; Sat–Sun 07:00–21:00 |
| `5eed0000-0000-4000-8000-000000000003` | SportMate Community Court | Active | Thu Duc City | **none** (active court with no sports) | Mon–Sat 06:00–20:00; closed Sunday |
| `5eed0000-0000-4000-8000-000000000004` | SportMate Training Court | Active | Hanoi | Badminton 90,000 | **none** (active court with no operating hours) |
| `5eed0000-0000-4000-8000-000000000005` | SportMate Closed Court | **Inactive** | Ho Chi Minh City | Tennis 140,000 | Every day 06:00–22:00 |

All courts use the `Asia/Ho_Chi_Minh` time zone. The Closed Court must not appear in `GET /api/v1/discovery/courts`, and its discovery detail endpoints return 404.

Sports are the ones seeded by the migrations, looked up by slug (`tennis`, `pickleball`, `badminton`, `table-tennis`, `basketball`); no sports are created.

The courts need an owner. By default the script creates one account for that, `dev-seed-owner@sportmate.local` (CourtOwner). **It cannot log in**: its stored password hash is intentionally invalid, so every login attempt fails. To make one of your own accounts the owner instead, register it, promote it to CourtOwner (see the backend README), and change `SET @owner_email = …` at the top of the script before running it.

## Running it

Apply the migrations first (backend README), then run the script with the MySQL client that comes with MySQL Server, from the repository root. PowerShell:

```powershell
& "C:\Program Files\MySQL\MySQL Server 8.0\bin\mysql.exe" -u <user> -p sportmate -e "source backend/database/seed/dev-court-discovery.sql"
```

`-p` prompts for the password. Use the same account and database as your `ConnectionStrings:DefaultConnection`. In MySQL Workbench you can instead open the file and execute it.

The script ends by printing each seed court with its number of sports and operating periods:

```text
SportMate Tennis Center     Active    2  8
SportMate Badminton Arena   Active    3  7
SportMate Community Court   Active    0  6
SportMate Training Court    Active    1  0
SportMate Closed Court      Inactive  1  7
```

## Rerunning

Safe to run as often as you like; it never creates duplicates:

- Every seed row has a fixed id starting with `5eed0000-` and is inserted or updated in place. Court sports are also matched on the unique (court, sport) pair.
- Rerunning restores the seed courts' details, prices, status and operating hours to the values above (for example after you edited them in the app), and replaces any extra operating periods added to the seed courts.
- It never drops, truncates or deletes anything else, and does not touch other users, courts, sports or bookings. Extra court sports added to a seed court are left as they are.
- It runs in one transaction: if anything fails (for example `@owner_email` is not an active CourtOwner, or a sport is missing or inactive), nothing is changed.

To remove the seed data again, deactivate the seed courts (set `status = 2`) rather than deleting them, like any other court.
