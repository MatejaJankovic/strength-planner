using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StrengthPlanner.Application.Interfaces;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Infrastructure.Analytics;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Exercises;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Mesocycles;
using StrengthPlanner.Infrastructure.OneRepMax;
using StrengthPlanner.Infrastructure.Persistence;
using StrengthPlanner.Infrastructure.Templates;
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

                // Šest znakova bez ijedne dodatne provere je prihvatalo i "123456".
                // Dužina je jedina mera koja stvarno otežava pogađanje, pa se podiže; za
                // uslove složenosti se i dalje ne traži ništa skriveno od korisnika, jer
                // ih UI ne komunicira i jer teraju ljude na predvidive obrasce.
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireDigit = false;
                options.Password.RequiredLength = PasswordPolicy.MinimumLength;

                // Zaštita od brute-force pogađanja lozinke.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();

        // Lozinke hešira Identity (PBKDF2-HMAC-SHA512, so po lozinci). Broj iteracija je
        // jedini deo koji ima smisla dizati: on određuje koliko košta pogađanje ako baza
        // ikada iscuri. .NET 8 podrazumeva 100.000; OWASP za SHA-512 preporučuje 210.000.
        //
        // Izmena je unazad kompatibilna — broj iteracija je upisan u sam heš, pa se
        // zatečene lozinke i dalje proveravaju svojim starim brojem. Cena je jedno
        // heširanje po prijavi, a ne po zahtevu.
        services.Configure<PasswordHasherOptions>(options =>
        {
            options.IterationCount = 210_000;
        });

        // JWT postavke + servisi.
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddScoped<ICustomTemplateService, CustomTemplateService>();
        services.AddScoped<IWorkoutTemplateResolver, WorkoutTemplateResolver>();
        services.AddScoped<IMesocycleGenerator, MesocycleGenerator>();
        services.AddScoped<IMesocycleService, MesocycleService>();
        services.AddScoped<IMacrocycleService, MacrocycleService>();
        services.AddScoped<IOneRepMaxService, OneRepMaxService>();
        services.AddScoped<ISetLogService, SetLogService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<VolumeLandmarkService>();
        services.AddScoped<WeeklySetPlanner>();
        services.AddScoped<DeloadService>();
        services.AddScoped<IVolumeService, VolumeService>();
        services.AddScoped<IAnalyticsService, AnalyticsService>();

        return services;
    }
}
