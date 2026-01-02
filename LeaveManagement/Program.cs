using Hangfire;
using LeaveManagement;
using LeaveManagement.Jobs;
using LeaveManagement.Models;
using LeaveManagement.SeedData;
using LeaveManagement.VM;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog; // ✅ Add this

var builder = WebApplication.CreateBuilder(args);


//
// ✅ 1. Configure Serilog for console + file logging
//
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()

    .WriteTo.File(
        "Logs/log-.txt",                // folder path (auto-created)
        rollingInterval: RollingInterval.Day, // new file daily
        retainedFileCountLimit: 10,          // keep 10 most recent logs
        shared: true)
    .CreateLogger();

builder.Host.UseSerilog(); // use Serilog as logging provider


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<LeaveDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<LeaveDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddScoped< EmailService>();

// ✅ Add Hangfire
builder.Services.AddHangfire(config =>
{
    config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"));
});
builder.Services.AddHangfireServer();


builder.Services.ConfigureApplicationCookie(options =>
{
   
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";   
    options.ExpireTimeSpan = TimeSpan.FromHours(8); // Example: 8 hours    
    options.SlidingExpiration = true; 
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddScoped<SalaryViewModel>();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<LeaveDbContext>();

    // ✅ Apply pending migrations automatically
    dbContext.Database.Migrate();
    await DbSeeder.SeedRolesAndAdminAsync(services);
}
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


//
// ✅ 5. Hangfire dashboard (optional but recommended)
//
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[]
    {
        new BasicAuthAuthorizationFilter("test@123.com", "admin")
    }
});

//
// ✅ 6. Schedule recurring job (9:00 AM IST daily)
//
RecurringJob.AddOrUpdate<DailyEmailJob>(
    "daily-email-job",
    job => job.RunAsync(),
    "30 3 * * *", // 9:00 AM IST (UTC +5:30)
    TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
);
// ✅ Testing: run job every 1 minute
//RecurringJob.AddOrUpdate<DailyEmailJob>(
//    "daily-email-job",
//    job => job.RunAsync(),
//    Cron.Minutely(),
//    TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
//);


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
