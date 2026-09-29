using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SportMate.Application;
using SportMate.Application.Auth;
using SportMate.Infrastructure.Persistence;
using SportMate.Infrastructure.Security;

namespace SportMate.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSportMateInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // 8.0.16 is the minimum MySQL version that enforces CHECK constraints.
        services.AddDbContext<SportMateDbContext>(options =>
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 16))));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<SportMateDbContext>());

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

        return services;
    }
}
