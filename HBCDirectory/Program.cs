using HBCDirectory.Data;
using HBCDirectory.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<PhotoService>();
builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
    });

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

builder.Services.AddDbContext<DirectoryContext>(options => options.UseNpgsql(connectionString));
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