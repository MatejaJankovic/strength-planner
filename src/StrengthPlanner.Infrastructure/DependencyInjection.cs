using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.OneRepMax;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.TrainingLogs;

namespace StrengthPlanner.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registruje EF Core (PostgreSQL / Npgsql), ASP.NET Identity i auth servise.
    /// JWT bearer autentifikacija (validacija tokena) konfiguriše se u API/Program.cs.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nije podešen.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        // ASP.NET Identity (bez cookie UI-ja; login ide preko UserManager + JWT).
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Usaglašeno sa UI porukom "bar 6 karaktera" — bez skrivenih
                // dodatnih zahteva koje frontend ne komunicira.
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = 6;

                // Zaštita od brute-force pogađanja lozinke.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        // JWT postavke + servisi.
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IMesocycleGenerator, MesocycleGenerator>();
        services.AddScoped<IMesocycleService, MesocycleService>();
        services.AddScoped<IOneRepMaxService, OneRepMaxService>();
        services.AddScoped<ISetLogService, SetLogService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IVolumeService, VolumeService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        return services;
    }
}
