using LoanSystemAPI.Data;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // THE Authorize BUTTON OF SWAGGER SENDS THE ACCESS TOKEN AS "Bearer <token>"
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Access token returned by api/auth/login",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
    });
});

//DB CONECTION
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidOperationException("ConnectionString Not Found");
    ;
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>()));

//SERVICES
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalDateService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<LoanBalanceService>();
builder.Services.AddSingleton<AuditLogWriter>();
builder.Services.AddScoped<AuditInterceptor>();
builder.Services.AddScoped<JobRunner>();
builder.Services.AddHostedService<DailyJobsService>();
builder.Services.AddScoped<IDailyJob, RefreshTokenCleanupJob>();
builder.Services.AddScoped<IDailyJob, AuditLogPurgeJob>();

//AUTHENTICATION
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthTokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IConfiguration, TimeProvider>((options, configuration, timeProvider) =>
    {
        // THE CLAIMS KEEP THEIR JWT NAMES (sub, role) INSTEAD OF BEING RENAMED TO THE LONG .NET ONES
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = AuthTokenService.GetSigningKey(configuration),
            NameClaimType = AuthTokenService.UsernameClaim,
            RoleClaimType = AuthTokenService.RoleClaim,
            // THE EXPIRATION IS CHECKED WITH THE SAME CLOCK THAT CREATES THE TOKENS, WITHOUT TOLERANCE (D-062)
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = timeProvider.GetUtcNow().UtcDateTime;
                return (notBefore == null || notBefore <= now) && expires != null && expires > now;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    // EVERY ENDPOINT NEEDS A LOGGED USER, UNLESS IT SAYS [AllowAnonymous]
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();

// PUBLIC SO THE INTEGRATION TESTS CAN START THE API WITH WebApplicationFactory<Program>
public partial class Program { }
