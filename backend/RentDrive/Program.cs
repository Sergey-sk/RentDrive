using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentDrive;
using RentDrive.background;
using RentDrive.db;
using RentDrive.db.models;
using RentDrive.dto.authDto;
using RentDrive.dto.bookingsDto;
using RentDrive.endpoints;
using RentDrive.services.implementations;
using RentDrive.services.interfaces;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var serverVersion = new MySqlServerVersion(new Version(8, 0, 33));
    options.UseMySql(connectionString, serverVersion);
});

builder.Services.AddAntiforgery();

builder.Services.AddIdentityApiEndpoints<User>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.User.RequireUniqueEmail = true;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;

    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<CompleteProfileDtoValidator>();

builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme);

builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
{
    options.LoginPath = "/auth/custom-login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", opt => opt.RequireRole("Admin"));
});

builder.Services.AddOpenApi();
builder.Services.AddTransient<IEmailSender<User>, EmailSender>();
builder.Services.AddScoped<IItemsService, ItemsService>();
builder.Services.AddScoped<IBookingsService, BookingsService>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddSingleton<IFileDeleteQueue, FileDeleteQueue>();
builder.Services.AddHostedService<FileDeleteWorker>();
builder.Services.AddHostedService<DatabaseImagesCleanupWorker>();
builder.Services.AddHostedService<BookingStatusWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("RentDrive API")
        .WithTheme(ScalarTheme.Alternate)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapItemsEndpoint();
app.MapBookingsEndpoints();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        await IdentitySeedData.InitializeAsync(services, dbContext);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Произошла ошибка при инициализации ролей в БД.");
    }
}

app.Run();