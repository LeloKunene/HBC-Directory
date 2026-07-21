using HBCDirectory.Data;
using HBCDirectory.Services;
using QuestPDF.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddRazorPages();
builder.Services.AddSingleton<PhotoService>();
builder.Services.AddScoped<DirectoryPdfService>();
builder.Services.AddScoped<HBCDirectory.Services.TokenService>();
builder.Services.AddScoped<HBCDirectory.Services.EmailService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
    });

/* Rate limiting — protects the login endpoint from brute-force attempts and
    caps how often the (relatively expensive) PDF download/generation paths
    can be hit per user/IP. Policy names are referenced via [EnableRateLimiting]
    on the relevant PageModels.*/
builder.Services.AddRateLimiter(options =>
{
    // PDF download: 10 per signed-in user per hour.
    options.AddPolicy("pdf", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name
                ?? httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromHours(1),
                QueueLimit  = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

builder.Services.AddKeyedSingleton<PartitionedRateLimiter<HttpContext>>("login", (_, _) =>
    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            })));

/*PDF generation (admin-triggered, R2 write + QuestPDF render): 10/hour.
    Registered as a standalone keyed limiter rather than an [EnableRateLimiting]
    policy: Admin.cshtml has many OnPost* handlers that all share the single
    "/Admin" route, so a page-level policy would throttle every admin action
    (adding a member, editing a family, etc.) together with PDF generation.
    OnPostGeneratePdfAsync acquires a lease from this limiter directly instead.*/
builder.Services.AddKeyedSingleton<RateLimiter>("pdfgenerate", (_, _) =>
    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window      = TimeSpan.FromHours(1),
        QueueLimit  = 0
    }));

var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=hbc.db";
var pgHost = Environment.GetEnvironmentVariable("PGHOST");
if (!string.IsNullOrEmpty(pgHost))
{
    connectionString = $"Host={pgHost};" +
        $"Port={Environment.GetEnvironmentVariable("PGPORT") ?? "5432"};" +
        $"Database={Environment.GetEnvironmentVariable("PGDATABASE")};" +
        $"Username={Environment.GetEnvironmentVariable("PGUSER")};" +
        $"Password={Environment.GetEnvironmentVariable("PGPASSWORD")};" +
        $"SSL Mode=Require;Trust Server Certificate=true";
}

QuestPDF.Settings.License = LicenseType.Community;
builder.Services.AddDbContextFactory<DirectoryContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<DirectoryContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<DirectoryContext>>().CreateDbContext());

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var app = builder.Build();

// Ensure database and uploads folder
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DirectoryContext>();
    db.Database.Migrate();
    var uploads = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "uploads");
    if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Use(async (context, next) =>
{
    context.Request.Scheme = "https";
    await next();
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "5272";
app.Run($"http://0.0.0.0:{port}");