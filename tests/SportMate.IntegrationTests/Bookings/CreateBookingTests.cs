using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using SportMate.Domain.Enums;
using SportMate.IntegrationTests.Courts;
using SportMate.IntegrationTests.Infrastructure;
using static SportMate.IntegrationTests.Infrastructure.BookingApi;

namespace SportMate.IntegrationTests.Bookings;

public sealed class CreateBookingTests(SportMateApiFactory factory) : IClassFixture<SportMateApiFactory>
{
    private static readonly DateOnly Day = LocalDate();

    // ---- creation ----

    [Fact]
    public async Task Create_WithoutToken_Returns401()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);

        using var response = await AuthApi.PostAsync(
            client, BasePath, Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_ReturnsPendingHoldWithPriceSnapshotAndLocalTimes()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var before = DateTime.UtcNow;

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:30")));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var booking = await ReadAsync(response);
        Assert.Equal($"/api/v1/bookings/{booking.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("pending", booking.Status);
        Assert.Equal(user.User.Id, booking.UserId);
        Assert.Equal(court.Court.Id, booking.CourtId);
        Assert.Equal(court.TennisCourtSportId, booking.CourtSportId);
        Assert.Equal(new SportDto(Tennis, "Tennis", "tennis", true), booking.Sport);
        Assert.Equal(Utc(Day, 3, 0), booking.StartsAtUtc);
        Assert.Equal(Utc(Day, 4, 30), booking.EndsAtUtc);
        Assert.Equal(VietnamZone, booking.TimeZoneId);
        Assert.Equal(Day.ToString("yyyy-MM-dd"), booking.LocalDate);
        Assert.Equal(("10:00", "11:30"), (booking.LocalStartTime, booking.LocalEndTime));
        Assert.Equal(150_000.00m, booking.TotalPrice); // 100,000/hour * 90 minutes
        Assert.Equal("VND", booking.Currency);
        Assert.InRange(booking.CreatedAtUtc, before, DateTime.UtcNow);
        Assert.Equal(booking.CreatedAtUtc.AddMinutes(15), booking.HoldExpiresAtUtc);
        Assert.Null(booking.CancelledAtUtc);

        await factory.WithDbAsync(async db =>
        {
            var stored = await db.Bookings.AsNoTracking().SingleAsync(b => b.Id == booking.Id);
            Assert.Equal(BookingStatus.Pending, stored.Status);
            Assert.Equal(court.Court.Id, stored.CourtId);
            Assert.Equal(150_000.00m, stored.TotalPrice);
            Assert.Equal(Utc(Day, 3, 0), stored.StartsAtUtc);
            Assert.Equal(DateTimeKind.Utc, stored.StartsAtUtc.Kind);
        });
    }

    [Theory]
    [InlineData(UserRole.User)]
    [InlineData(UserRole.CourtOwner)]
    [InlineData(UserRole.Admin)]
    public async Task EveryRole_CanBookForThemselves(UserRole role)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var caller = await CourtApi.LoginWithRoleAsync(factory, client, role);

        var booking = await CreateBookingAsync(client, caller.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        Assert.Equal(caller.User.Id, booking.UserId);
    }

    [Fact]
    public async Task CourtOwner_CanBookTheirOwnCourt()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);

        var booking = await CreateBookingAsync(client, court.Owner.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        Assert.Equal(court.Owner.User.Id, booking.UserId);
    }

    [Fact]
    public async Task Create_IgnoresClientSuppliedIdentityCourtPriceAndStatus()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var otherCourt = await ArrangeCourtAsync(factory, client);
        var alice = await AuthApi.RegisterAndLoginAsync(client);
        var bob = await AuthApi.RegisterAndLoginAsync(client);

        var booking = await CreateBookingAsync(client, bob.AccessToken, new
        {
            court_sport_id = court.TennisCourtSportId,
            starts_at = VietnamTime(Day, "10:00"),
            ends_at = VietnamTime(Day, "11:00"),
            user_id = alice.User.Id,
            court_id = otherCourt.Court.Id,
            total_price = 1m,
            price_per_hour = 1m,
            currency = "USD",
            status = "confirmed",
            created_at_utc = "2000-01-01T00:00:00Z"
        });

        Assert.Equal(bob.User.Id, booking.UserId);
        Assert.Equal(court.Court.Id, booking.CourtId);
        Assert.Equal(TennisPrice, booking.TotalPrice);
        Assert.Equal("VND", booking.Currency);
        Assert.Equal("pending", booking.Status);
        Assert.True(booking.CreatedAtUtc.Year > 2000);
    }

    [Fact]
    public async Task Create_UnknownCourtSport_Returns404()
    {
        using var client = factory.CreateClient();
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(Guid.NewGuid(), VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutCourtSport_Returns400()
    {
        using var client = factory.CreateClient();
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            new { starts_at = VietnamTime(Day, "10:00"), ends_at = VietnamTime(Day, "11:00") });

        await CreateCourtTests.AssertValidationErrorAsync(response, "court_sport_id");
    }

    // ---- timestamps ----

    [Theory]
    [InlineData("T10:00:00+07:00", "T11:00:00+07:00")]
    [InlineData("T03:00:00Z", "T04:00:00Z")]
    [InlineData("T03:00Z", "T04:00Z")]
    [InlineData("T12:00:00+09:00", "T04:00:00Z")] // mixed offsets, same instants
    public async Task Create_AcceptsZAndExplicitOffsets(string start, string end)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{Day:yyyy-MM-dd}{start}", $"{Day:yyyy-MM-dd}{end}"));

        Assert.Equal(Utc(Day, 3, 0), booking.StartsAtUtc);
        Assert.Equal(Utc(Day, 4, 0), booking.EndsAtUtc);
        Assert.Equal(("10:00", "11:00"), (booking.LocalStartTime, booking.LocalEndTime));
    }

    [Theory]
    [InlineData("T10:00:00", "T11:00:00+07:00", "starts_at")]     // time-zone-less
    [InlineData("T10:00:00+07:00", "T11:00:00", "ends_at")]
    [InlineData("T10:00:30+07:00", "T11:00:00+07:00", "starts_at")] // seconds
    [InlineData("T10:00:00+07:00", "T11:00:00.250+07:00", "ends_at")] // fraction
    [InlineData("T11:00:00+07:00", "T11:00:00+07:00", "ends_at")]   // start == end
    [InlineData("T11:00:00+07:00", "T10:00:00+07:00", "ends_at")]   // start > end
    public async Task Create_WithInvalidTimestamps_Returns400(string start, string end, string field)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{Day:yyyy-MM-dd}{start}", $"{Day:yyyy-MM-dd}{end}"));

        await CreateCourtTests.AssertValidationErrorAsync(response, field);
        await AssertNoBookingsAsync(court.Court.Id);
    }

    [Fact]
    public async Task Create_StartingNowOrInThePast_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, periods: [("00:00", "23:59")]);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var thisMinute = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);

        using var now = await CreateAsync(client, user.AccessToken, Body(court.TennisCourtSportId, Iso(thisMinute), Iso(thisMinute.AddMinutes(30))));
        using var past = await CreateAsync(client, user.AccessToken, Body(court.TennisCourtSportId, Iso(thisMinute.AddDays(-1)), Iso(thisMinute.AddDays(-1).AddHours(1))));

        await CreateCourtTests.AssertValidationErrorAsync(now, "starts_at");
        await CreateCourtTests.AssertValidationErrorAsync(past, "starts_at");
    }

    // ---- operating hours ----

    [Theory]
    [InlineData("10:00", "11:00")] // inside one period
    [InlineData("06:00", "12:00")] // exactly one period
    [InlineData("11:00", "13:30")] // across touching periods 06-12 and 12-14
    [InlineData("06:00", "14:00")] // the whole touching span
    [InlineData("15:00", "22:00")]
    public async Task Create_InsideOperatingHours_Succeeds(string start, string end)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, start), VietnamTime(Day, end)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Theory]
    [InlineData("13:30", "15:30")] // crosses the 14:00-15:00 gap
    [InlineData("14:00", "15:00")] // only the gap
    [InlineData("05:30", "06:30")] // starts before opening
    [InlineData("21:30", "22:30")] // ends after closing
    [InlineData("22:00", "23:00")] // after closing
    public async Task Create_OutsideOperatingHours_Returns400(string start, string end)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, start), VietnamTime(Day, end)));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
        await AssertNoBookingsAsync(court.Court.Id);
    }

    [Fact]
    public async Task Create_OnDayWithoutOperatingHours_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, days: [Day.AddDays(1).DayOfWeek]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
    }

    [Fact]
    public async Task Create_WhenCourtHasNoOperatingHoursAtAll_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, days: []);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
    }

    [Fact]
    public async Task Create_CrossingLocalMidnight_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, periods: [("00:00", "23:59")]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "23:00"), VietnamTime(Day.AddDays(1), "01:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "ends_at");
    }

    // ---- time zone ----

    [Fact]
    public async Task OperatingHours_UseTheCourtsLocalDay_NotTheUtcDay()
    {
        using var client = factory.CreateClient();
        // Only the local day has hours, early in the morning: 06:30 local is 23:30 UTC of the previous day.
        var court = await ArrangeCourtAsync(factory, client, periods: [("06:00", "08:00")], days: [Day.DayOfWeek]);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var previousUtcDay = Day.AddDays(-1);

        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{previousUtcDay:yyyy-MM-dd}T23:30:00Z", $"{Day:yyyy-MM-dd}T00:30:00Z"));

        Assert.Equal(Day.ToString("yyyy-MM-dd"), booking.LocalDate);
        Assert.Equal(("06:30", "07:30"), (booking.LocalStartTime, booking.LocalEndTime));
    }

    [Fact]
    public async Task OperatingHours_OfTheUtcDayDoNotApply()
    {
        using var client = factory.CreateClient();
        // Hours exist only on the UTC weekday of the instant; the local weekday (one day later) has none.
        var court = await ArrangeCourtAsync(factory, client, periods: [("00:00", "23:59")], days: [Day.AddDays(-1).DayOfWeek]);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var previousUtcDay = Day.AddDays(-1);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{previousUtcDay:yyyy-MM-dd}T23:30:00Z", $"{Day:yyyy-MM-dd}T00:30:00Z"));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
    }

    [Fact]
    public async Task DaylightSaving_BookingAcrossSkippedLocalHour_Returns400()
    {
        const string zoneId = "America/New_York";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var springForward = FindFutureDate(date => zone.IsInvalidTime(date.ToDateTime(new TimeOnly(2, 30))));
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, zoneId, periods: [("00:00", "23:59")]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        // 01:30 EST -> 03:30 EDT: local 02:00-03:00 does not exist that day.
        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{springForward:yyyy-MM-dd}T01:30:00-05:00", $"{springForward:yyyy-MM-dd}T03:30:00-04:00"));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
        await AssertNoBookingsAsync(court.Court.Id);
    }

    [Fact]
    public async Task DaylightSaving_BookingAtAmbiguousLocalTime_Returns400()
    {
        const string zoneId = "America/New_York";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var fallBack = FindFutureDate(date => zone.IsAmbiguousTime(date.ToDateTime(new TimeOnly(1, 30))));
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, zoneId, periods: [("00:00", "23:59")]);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        // 01:30 happens twice that night (EDT, then EST).
        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, $"{fallBack:yyyy-MM-dd}T01:30:00-04:00", $"{fallBack:yyyy-MM-dd}T01:45:00-04:00"));

        await CreateCourtTests.AssertValidationErrorAsync(response, "starts_at");
    }

    [Fact]
    public async Task DaylightSavingZone_OrdinaryDay_ConvertsWithTheZonesOffset()
    {
        const string zoneId = "America/New_York";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var date = LocalDate(zoneId, 10);
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client, zoneId, periods: [("09:00", "17:00")]);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(10, 0)), zone);

        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, Iso(startUtc), Iso(startUtc.AddHours(1))));

        Assert.Equal(zoneId, booking.TimeZoneId);
        Assert.Equal(date.ToString("yyyy-MM-dd"), booking.LocalDate);
        Assert.Equal(("10:00", "11:00"), (booking.LocalStartTime, booking.LocalEndTime));
    }

    // ---- active state ----

    [Fact]
    public async Task Create_OnInactiveCourt_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        using (await CourtApi.UpdateAsync(client, court.Admin.AccessToken, court.Court.Id, CourtApi.UpdateBodyFrom(court.Court, "inactive")))
        {
        }

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "court_sport_id");
    }

    [Fact]
    public async Task Create_ForInactiveCourtSport_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        using (await client.SendAsync(AuthApi.Authorized(HttpMethod.Delete, $"/api/v1/courts/{court.Court.Id}/sports/{Tennis}", court.Admin.AccessToken)))
        {
        }

        using var response = await CreateAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "court_sport_id");
    }

    [Fact]
    public async Task Create_ForInactiveSport_Returns400()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var sport = await factory.AddSportAsync();
        var courtSport = new SportMate.Domain.Entities.CourtSport(court.Court.Id, sport.Id, 50_000m, "VND", DateTime.UtcNow);
        await factory.WithDbAsync(async db =>
        {
            db.CourtSports.Add(courtSport);
            await db.SaveChangesAsync();
        });
        await factory.SetSportActiveAsync(sport.Id, false);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        using var response = await CreateAsync(client, user.AccessToken,
            Body(courtSport.Id, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        await CreateCourtTests.AssertValidationErrorAsync(response, "court_sport_id");
    }

    [Fact]
    public async Task DeactivatingCourtOrSport_LeavesExistingBookingsUnchanged()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        using (await CourtApi.UpdateAsync(client, court.Admin.AccessToken, court.Court.Id, CourtApi.UpdateBodyFrom(court.Court, "inactive")))
        {
        }

        using (await client.SendAsync(AuthApi.Authorized(HttpMethod.Delete, $"/api/v1/courts/{court.Court.Id}/sports/{Tennis}", court.Admin.AccessToken)))
        {
        }

        using var get = await GetAsync(client, user.AccessToken, PathOf(booking.Id));
        var after = await ReadAsync(get);
        Assert.Equal("pending", after.Status);
        Assert.Equal(booking.UpdatedAtUtc, after.UpdatedAtUtc);
    }

    // ---- price ----

    [Theory]
    [InlineData("10:00", "11:00", "80000.00")]
    [InlineData("10:00", "10:45", "60000.00")]
    [InlineData("10:00", "10:07", "9333.33")] // 9333.333...
    [InlineData("06:00", "14:00", "640000.00")]
    public async Task Create_CalculatesPriceFromTheCourtSportsHourlyPrice(string start, string end, string expected)
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);

        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.BadmintonCourtSportId, VietnamTime(Day, start), VietnamTime(Day, end)));

        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), booking.TotalPrice);
        Assert.Equal("VND", booking.Currency);
    }

    [Fact]
    public async Task Create_RoundsHalfCentsAwayFromZero()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        using (await client.SendAsync(AuthApi.Authorized(HttpMethod.Put, $"/api/v1/courts/{court.Court.Id}/sports/{Tennis}",
                   court.Admin.AccessToken, new { price_per_hour = 0.01m, currency = "usd", is_active = true })))
        {
        }

        var user = await AuthApi.RegisterAndLoginAsync(client);

        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "10:30")));

        Assert.Equal(0.01m, booking.TotalPrice); // 0.005 -> 0.01, not banker's 0.00
        Assert.Equal("USD", booking.Currency);
    }

    [Fact]
    public async Task LaterPriceChange_DoesNotAlterExistingBooking()
    {
        using var client = factory.CreateClient();
        var court = await ArrangeCourtAsync(factory, client);
        var user = await AuthApi.RegisterAndLoginAsync(client);
        var booking = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "10:00"), VietnamTime(Day, "11:00")));

        using (await client.SendAsync(AuthApi.Authorized(HttpMethod.Put, $"/api/v1/courts/{court.Court.Id}/sports/{Tennis}",
                   court.Admin.AccessToken, new { price_per_hour = 999_000m, currency = "USD", is_active = true })))
        {
        }

        using var get = await GetAsync(client, user.AccessToken, PathOf(booking.Id));
        var after = await ReadAsync(get);
        Assert.Equal(TennisPrice, after.TotalPrice);
        Assert.Equal("VND", after.Currency);

        var next = await CreateBookingAsync(client, user.AccessToken,
            Body(court.TennisCourtSportId, VietnamTime(Day, "11:00"), VietnamTime(Day, "12:00")));
        Assert.Equal(999_000m, next.TotalPrice);
        Assert.Equal("USD", next.Currency);
    }

    // ---- helpers ----

    private static DateTime Utc(DateOnly vietnamDate, int hour, int minute) =>
        DateTime.SpecifyKind(vietnamDate.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Utc);

    private static string Iso(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static DateOnly FindFutureDate(Func<DateOnly, bool> predicate)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        for (var i = 0; i < 800; i++, date = date.AddDays(1))
        {
            if (predicate(date))
            {
                return date;
            }
        }

        throw new InvalidOperationException("No matching date found.");
    }

    private Task AssertNoBookingsAsync(Guid courtId) => factory.WithDbAsync(async db =>
        Assert.False(await db.Bookings.AnyAsync(b => b.CourtId == courtId)));
}
