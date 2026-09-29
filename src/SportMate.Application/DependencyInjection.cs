using Microsoft.Extensions.DependencyInjection;
using SportMate.Application.Auth;
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
        return services;
    }
}
