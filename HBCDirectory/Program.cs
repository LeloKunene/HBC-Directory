using HBCDirectory.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
    });

// Use SQLite file database in content root
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=hbc.db";
builder.Services.AddDbContext<DirectoryContext>(options => options.UseSqlite(connectionString));

var app = builder.Build();

// Ensure database and uploads folder
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DirectoryContext>();
    db.Database.EnsureCreated();
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

app.Run();
