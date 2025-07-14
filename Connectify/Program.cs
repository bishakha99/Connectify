using Connectify.DAL;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Connectify.Hubs; // We will create this later
using Connectify.Services; // We will create this later

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Database Context (Dependency Injection)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ConnectifyDb>(options =>
    options.UseNpgsql(connectionString));

// 2. Add MVC services
builder.Services.AddControllersWithViews();

// 3. Configure Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Redirect to this page if user is not authenticated
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

// 4. Add SignalR for real-time communication
builder.Services.AddSignalR();

// 5. Add custom services for dependency injection
builder.Services.AddScoped<IEmailService, EmailService>(); // For sending emails
builder.Services.AddSingleton<IUserConnectionManager, UserConnectionManager>(); // For tracking online users

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // To serve CSS, JavaScript, and images

app.UseRouting();

// IMPORTANT: Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<ChatHub>("/chatHub"); // We will create ChatHub later

app.Run();