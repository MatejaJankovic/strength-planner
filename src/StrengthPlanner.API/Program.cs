using System.Text;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StrengthPlanner.Application.Exceptions;
using StrengthPlanner.Infrastructure;
using StrengthPlanner.Infrastructure.Authentication;
using StrengthPlanner.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// CORS: Angular dev server (radi se kasnije) sme da poziva ovaj API.
// ---------------------------------------------------------------------------
const string AllowAngular = "AllowAngular";
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key))
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

app.UseCors(AllowAngular);

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
