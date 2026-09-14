using LoanSystemAPI.Data;
using LoanSystemAPI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//DB CONECTION
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
    throw new InvalidOperationException("ConnectionString Not Found");
    ;
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()); 

//SERVICES
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<LocalDateService>();
builder.Services.AddScoped<CashService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<LoanBalanceService>();
builder.Services.AddScoped<JobRunner>();
builder.Services.AddHostedService<DailyJobsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// PUBLIC SO THE INTEGRATION TESTS CAN START THE API WITH WebApplicationFactory<Program>
public partial class Program { }
