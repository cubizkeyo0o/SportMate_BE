using Microsoft.Extensions.DependencyInjection;
using SportMate.Application.Auth;
using SportMate.Application.Users;

namespace SportMate.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSportMateApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        return services;
    }
}
