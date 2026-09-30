-- =====================================================================================================
-- SportMate DEVELOPMENT seed data: courts for testing court discovery (Home -> Courts -> Court detail).
--
-- Development/test data only. Never run against a production database.
-- Not a migration and not run by the application or by the automated tests.
--
-- Safe to run repeatedly: every row has a fixed id (prefix 5eed0000-) and is inserted or updated in
-- place, so reruns never create duplicates. It only touches its own rows: nothing is dropped or
-- truncated, and no other user, court or sport is changed.
--
-- Requires the EF Core migrations to be applied first (the sports it uses are seeded by them).
-- See database/seed/README.md for how to run it.
-- =====================================================================================================

-- Owner of the seed courts. By default a dedicated seed account (created below) that CANNOT log in.
-- To own the courts with one of your own CourtOwner accounts instead, change this before running;
-- that account must already exist and be an active CourtOwner (role = 2, status = 1).
SET @owner_email = 'dev-seed-owner@sportmate.local';

SET @now = UTC_TIMESTAMP(6);

START TRANSACTION;

-- -----------------------------------------------------------------------------------------------------
-- Seed owner account: role CourtOwner (2), status Active (1). The password hash 'AA==' is deliberately
-- not a valid ASP.NET Core Identity hash, so every login attempt for this account fails.
-- Left unchanged if it already exists.
-- -----------------------------------------------------------------------------------------------------
INSERT INTO users (id, email, password_hash, display_name, phone_number, avatar_url, role, status, created_at_utc, updated_at_utc)
VALUES ('5eed0000-0000-4000-8000-0000000000a1', 'dev-seed-owner@sportmate.local', 'AA==', 'Dev Seed Court Owner', NULL, NULL, 2, 1, @now, @now)
ON DUPLICATE KEY UPDATE id = id;

-- Resolves to NULL (and the court inserts below then fail on the NOT NULL owner_id, rolling back
-- everything) if @owner_email is not an active CourtOwner.
SET @owner_id = (SELECT id FROM users WHERE email = @owner_email AND role = 2 AND status = 1);

-- -----------------------------------------------------------------------------------------------------
-- Courts. status: 1 = Active, 2 = Inactive.
-- -----------------------------------------------------------------------------------------------------
INSERT INTO courts (id, owner_id, name, description, address, city, latitude, longitude, time_zone_id, status, created_at_utc, updated_at_utc)
VALUES
    ('5eed0000-0000-4000-8000-000000000001', @owner_id, 'SportMate Tennis Center',
     'Two floodlit hard courts with seating and changing rooms. Tennis and pickleball, rackets for rent at the front desk.',
     '123 Nguyen Van Linh, District 7', 'Ho Chi Minh City', 10.729500, 106.721000, 'Asia/Ho_Chi_Minh', 1, @now, @now),
    ('5eed0000-0000-4000-8000-000000000002', @owner_id, 'SportMate Badminton Arena',
     'Indoor air-conditioned hall with a wooden floor, used for badminton, table tennis and basketball.',
     '456 Vo Van Kiet, District 1', 'Ho Chi Minh City', 10.762600, 106.696000, 'Asia/Ho_Chi_Minh', 1, @now, @now),
    ('5eed0000-0000-4000-8000-000000000003', @owner_id, 'SportMate Community Court',
     'A simple neighbourhood community court. No sports are offered for online booking yet.',
     '789 Le Van Viet, Thu Duc City', 'Thu Duc City', 10.845200, 106.779700, 'Asia/Ho_Chi_Minh', 1, @now, @now),
    ('5eed0000-0000-4000-8000-000000000004', @owner_id, 'SportMate Training Court',
     'Coaching and training court. Opening hours have not been published yet.',
     '100 Cau Giay, Hanoi', 'Hanoi', 21.036200, 105.790600, 'Asia/Ho_Chi_Minh', 1, @now, @now),
    ('5eed0000-0000-4000-8000-000000000005', @owner_id, 'SportMate Closed Court',
     'Closed for renovation. Inactive, so it must not appear in court discovery.',
     '999 Example Street', 'Ho Chi Minh City', 10.776900, 106.700900, 'Asia/Ho_Chi_Minh', 2, @now, @now)
ON DUPLICATE KEY UPDATE
    owner_id = VALUES(owner_id),
    name = VALUES(name),
    description = VALUES(description),
    address = VALUES(address),
    city = VALUES(city),
    latitude = VALUES(latitude),
    longitude = VALUES(longitude),
    time_zone_id = VALUES(time_zone_id),
    status = VALUES(status),
    updated_at_utc = @now;

-- -----------------------------------------------------------------------------------------------------
-- Court sports. Sports are the ones seeded by the migrations, looked up by slug (never by id); a
-- missing or inactive sport resolves to NULL and fails the insert. Prices are VND per hour.
-- The unique (court_id, sport_id) index makes a rerun update the existing row instead of adding one.
-- The Community Court (…003) intentionally has none.
-- -----------------------------------------------------------------------------------------------------
INSERT INTO court_sports (id, court_id, sport_id, price_per_hour, currency, is_active, created_at_utc, updated_at_utc)
VALUES
    ('5eed0000-0000-4000-8001-000000000001', '5eed0000-0000-4000-8000-000000000001', (SELECT id FROM sports WHERE slug = 'tennis' AND is_active = 1),       150000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000002', '5eed0000-0000-4000-8000-000000000001', (SELECT id FROM sports WHERE slug = 'pickleball' AND is_active = 1),   120000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000003', '5eed0000-0000-4000-8000-000000000002', (SELECT id FROM sports WHERE slug = 'badminton' AND is_active = 1),    100000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000004', '5eed0000-0000-4000-8000-000000000002', (SELECT id FROM sports WHERE slug = 'table-tennis' AND is_active = 1),  80000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000005', '5eed0000-0000-4000-8000-000000000002', (SELECT id FROM sports WHERE slug = 'basketball' AND is_active = 1),   180000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000006', '5eed0000-0000-4000-8000-000000000004', (SELECT id FROM sports WHERE slug = 'badminton' AND is_active = 1),     90000.00, 'VND', 1, @now, @now),
    ('5eed0000-0000-4000-8001-000000000007', '5eed0000-0000-4000-8000-000000000005', (SELECT id FROM sports WHERE slug = 'tennis' AND is_active = 1),       140000.00, 'VND', 1, @now, @now)
ON DUPLICATE KEY UPDATE
    price_per_hour = VALUES(price_per_hour),
    currency = VALUES(currency),
    is_active = VALUES(is_active),
    updated_at_utc = @now;

-- -----------------------------------------------------------------------------------------------------
-- Operating hours: court-local wall-clock times. day_of_week: 0 = Sunday ... 6 = Saturday.
-- Periods of the seed courts that are not part of this seed are removed first (operating hours are
-- configuration, not history), so the schedules below are exactly what the seed courts end up with.
-- The Training Court (…004) intentionally has none.
-- -----------------------------------------------------------------------------------------------------
DELETE FROM court_operating_hours
WHERE court_id IN ('5eed0000-0000-4000-8000-000000000001', '5eed0000-0000-4000-8000-000000000002',
                   '5eed0000-0000-4000-8000-000000000003', '5eed0000-0000-4000-8000-000000000004',
                   '5eed0000-0000-4000-8000-000000000005')
  AND id NOT LIKE '5eed0000-0000-4000-8002-%';

INSERT INTO court_operating_hours (id, court_id, day_of_week, opens_at, closes_at, created_at_utc, updated_at_utc)
VALUES
    -- SportMate Tennis Center: Monday has two separate periods.
    ('5eed0000-0000-4000-8002-000000000101', '5eed0000-0000-4000-8000-000000000001', 1, '06:00:00', '12:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000102', '5eed0000-0000-4000-8000-000000000001', 1, '16:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000103', '5eed0000-0000-4000-8000-000000000001', 2, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000104', '5eed0000-0000-4000-8000-000000000001', 3, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000105', '5eed0000-0000-4000-8000-000000000001', 4, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000106', '5eed0000-0000-4000-8000-000000000001', 5, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000107', '5eed0000-0000-4000-8000-000000000001', 6, '07:00:00', '23:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000108', '5eed0000-0000-4000-8000-000000000001', 0, '07:00:00', '22:00:00', @now, @now),
    -- SportMate Badminton Arena: weekdays 06:00-22:00, weekend 07:00-21:00.
    ('5eed0000-0000-4000-8002-000000000201', '5eed0000-0000-4000-8000-000000000002', 1, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000202', '5eed0000-0000-4000-8000-000000000002', 2, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000203', '5eed0000-0000-4000-8000-000000000002', 3, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000204', '5eed0000-0000-4000-8000-000000000002', 4, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000205', '5eed0000-0000-4000-8000-000000000002', 5, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000206', '5eed0000-0000-4000-8000-000000000002', 6, '07:00:00', '21:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000207', '5eed0000-0000-4000-8000-000000000002', 0, '07:00:00', '21:00:00', @now, @now),
    -- SportMate Community Court: Monday-Saturday 06:00-20:00, closed on Sunday.
    ('5eed0000-0000-4000-8002-000000000301', '5eed0000-0000-4000-8000-000000000003', 1, '06:00:00', '20:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000302', '5eed0000-0000-4000-8000-000000000003', 2, '06:00:00', '20:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000303', '5eed0000-0000-4000-8000-000000000003', 3, '06:00:00', '20:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000304', '5eed0000-0000-4000-8000-000000000003', 4, '06:00:00', '20:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000305', '5eed0000-0000-4000-8000-000000000003', 5, '06:00:00', '20:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000306', '5eed0000-0000-4000-8000-000000000003', 6, '06:00:00', '20:00:00', @now, @now),
    -- SportMate Closed Court (inactive): every day 06:00-22:00.
    ('5eed0000-0000-4000-8002-000000000501', '5eed0000-0000-4000-8000-000000000005', 0, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000502', '5eed0000-0000-4000-8000-000000000005', 1, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000503', '5eed0000-0000-4000-8000-000000000005', 2, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000504', '5eed0000-0000-4000-8000-000000000005', 3, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000505', '5eed0000-0000-4000-8000-000000000005', 4, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000506', '5eed0000-0000-4000-8000-000000000005', 5, '06:00:00', '22:00:00', @now, @now),
    ('5eed0000-0000-4000-8002-000000000507', '5eed0000-0000-4000-8000-000000000005', 6, '06:00:00', '22:00:00', @now, @now)
ON DUPLICATE KEY UPDATE
    day_of_week = VALUES(day_of_week),
    opens_at = VALUES(opens_at),
    closes_at = VALUES(closes_at),
    updated_at_utc = @now;

COMMIT;

-- Summary of what the seed courts now contain.
SELECT c.name,
       CASE c.status WHEN 1 THEN 'Active' ELSE 'Inactive' END AS status,
       (SELECT COUNT(*) FROM court_sports cs WHERE cs.court_id = c.id) AS sports,
       (SELECT COUNT(*) FROM court_operating_hours h WHERE h.court_id = c.id) AS periods
FROM courts c
WHERE c.id LIKE '5eed0000-0000-4000-8000-%'
ORDER BY c.id;
