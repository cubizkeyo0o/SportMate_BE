using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SportMate.Application.Auth;
using SportMate.Application.Bookings;
using SportMate.Application.Courts;
using SportMate.Application.Sports;
using SportMate.Application.Users;

namespace SportMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSportMateApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<CourtService>();
        services.AddScoped<CourtSportService>();
        services.AddScoped<CourtOperatingHourService>();
        services.AddScoped<SportService>();
        services.AddScoped<UserSportProfileService>();
        services.AddScoped<BookingService>();

        services.AddOptions<BookingOptions>()
            .BindConfiguration(BookingOptions.SectionName)
            .Validate(o => o.HoldDurationMinutes is > 0 and <= 1440, "Booking:HoldDurationMinutes must be between 1 and 1440.")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BookingOptions>>().Value);
        return services;
    }
}
