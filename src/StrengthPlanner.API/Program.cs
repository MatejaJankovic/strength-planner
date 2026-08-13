using System.Text;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Infrastructure;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Identity;
using StrengthPlanner.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// CORS: Angular dev server (radi se kasnije) sme da poziva ovaj API.
// ---------------------------------------------------------------------------
const string AllowAngular = "AllowAngular";
const string AuthRateLimitPolicy = "auth";
builder.Services.AddCors(options =>
{
    options.AddPolicy(AllowAngular, policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ---------------------------------------------------------------------------
// MVC kontroleri + Swagger/OpenAPI (sa JWT "Authorize" dugmetom).
// ---------------------------------------------------------------------------
builder.Services.AddProblemDetails();

// ---------------------------------------------------------------------------
// Ograničenje broja zahteva na auth rutama.
//
// Zaključavanje naloga iz Identity-ja štiti JEDAN nalog. Ne sprečava probanje iste
// lozinke preko mnogo naloga, spam registracije (svaka troši PBKDF2 heširanje), ni
// namerno zaključavanje tuđeg naloga sa pet zahteva svakih pet minuta.
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(AuthRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Iza nginx-a je ovo prava adresa klijenta samo ako je obrada
            // X-Forwarded-For uključena (vidi UseForwardedHeaders ispod).
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problemDetails = new ValidationProblemDetails(context.ModelState)
        {
            Title = "Model validation failed.",
            Detail = "One or more request fields are invalid.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };

        return new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Unesi JWT ovako:  Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };
    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, scheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});

// ---------------------------------------------------------------------------
// Infrastruktura: EF Core (PostgreSQL / Npgsql), Identity, auth servisi.
// ---------------------------------------------------------------------------
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Autentifikacija: JWT bearer (validacija tokena koje pravi Infrastructure).
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("Jwt sekcija nije podešena u konfiguraciji.");

// Fail-fast na ključu. Dužina se traži u SVIM okruženjima: HS256 ispod 256 bita nije
// potpis nego ukras, a prazan ključ je ranije padao tek pri prvom izdavanju tokena, uz
// poruku iz koje se nije videlo šta nedostaje.
var jwtKey = jwtSettings.Key ?? string.Empty;

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key nije podešen ili je kraći od 32 bajta. U razvoju ga postavi kroz "
        + "user-secrets, u ostalim okruženjima kroz environment varijablu Jwt__Key.");
}

// Placeholder vrednosti se odbijaju samo van Development-a — pokrivaju ključeve iz
// .env.example i docker-compose.yml, koji su namerno prepoznatljivi.
if (!builder.Environment.IsDevelopment())
{
    string[] placeholderMarkers = { "change-me", "dev-only", "replace", "do-not-use", "example", "sample" };
    var looksLikePlaceholder = placeholderMarkers.Any(
        marker => jwtKey.Contains(marker, StringComparison.OrdinalIgnoreCase));

    if (looksLikePlaceholder)
    {
        throw new InvalidOperationException(
            "Jwt:Key liči na placeholder vrednost. Prosledi pravi, nasumičan ključ "
            + "kroz environment varijable ili tajne.");
    }
}

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // zadrži "sub"/"email" claim-ove kakvi jesu
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // JWT je bez stanja, pa odjava na klijentu ne poništava token — ukraden token je
        // do sada važio do isteka, čak i posle promene lozinke. Security stamp iz
        // Identity-ja se menja pri promeni lozinke, pa poređenje ovde daje pravo
        // poništavanje sesija po ceni jednog čitanja po zahtevu.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var stampInToken = context.Principal?.FindFirst(JwtTokenService.SecurityStampClaim)?.Value;
                var subject = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(stampInToken) || !Guid.TryParse(subject, out var userId))
                {
                    context.Fail("Token ne nosi podatke potrebne za proveru.");
                    return;
                }

                var users = context.HttpContext.RequestServices
                    .GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(userId.ToString());

                if (user is null || !string.Equals(user.SecurityStamp, stampInToken, StringComparison.Ordinal))
                {
                    context.Fail("Token više ne važi.");
                }
            }
        };
    });

// Globalno: svi endpoint-i traže autentifikaciju osim onih sa [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Startup: primeni migracije (uz retry dok baza ne postane dostupna) pa seed.
// Radi se UVEK (ne samo u Development-u) da bi "docker compose up" sam
// provizionisao šemu i ubacio seed podatke pri prvom pokretanju.
// ---------------------------------------------------------------------------
await app.Services.ApplyMigrationsAsync();
await DbSeeder.SeedAsync(app.Services);

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var problemDetails = CreateProblemDetails(context, app.Environment.IsDevelopment(), exception);

        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});

// ---------------------------------------------------------------------------
// HTTP request pipeline.
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // HTTPS redirect samo u lokalnom razvoju; u kontejneru API sluša čist HTTP
    // iza nginx proxy-ja (redirect bi ovde samo pravio problem).
    app.UseHttpsRedirection();
}

// Iza nginx-a je izvorna adresa klijenta samo u X-Forwarded-For. Bez ovoga bi sve
// zahteve ograničenje brojalo kao da dolaze sa jedne adrese — proxy-jeve.
// Objavljeni port API-ja je vezan za petlju (docker-compose), pa je nginx jedini put
// spolja i jedini koji ovo zaglavlje postavlja.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseCors(AllowAngular);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static ProblemDetails CreateProblemDetails(HttpContext context, bool includeDetails, Exception? exception)
{
    var (status, title) = exception switch
    {
        TrainingLogException { ErrorType: TrainingLogErrorType.Validation } => (StatusCodes.Status400BadRequest, "Validation failed."),
        MesocycleGenerationException => (StatusCodes.Status400BadRequest, "Mesocycle generation failed."),
        TrainingLogException { ErrorType: TrainingLogErrorType.NotFound } => (StatusCodes.Status404NotFound, "Resource was not found."),
        TrainingLogException { ErrorType: TrainingLogErrorType.Conflict } => (StatusCodes.Status409Conflict, "Request conflicts with current state."),
        UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized."),
        ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request."),
        _ => (StatusCodes.Status500InternalServerError, "Unexpected server error.")
    };

    return new ProblemDetails
    {
        Status = status,
        Title = title,
        Detail = includeDetails ? exception?.Message : title,
        Instance = context.Request.Path
    };
}
