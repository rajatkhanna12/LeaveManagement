using LeaveManagement.Controllers;

namespace LeaveManagement
{
    public class DailyEmailScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyEmailScheduler> _logger;

    

        public DailyEmailScheduler(IServiceProvider serviceProvider, ILogger<DailyEmailScheduler> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("✅ DailyEmailScheduler started at {Time}", DateTime.Now);

            
                // 🕘 Production mode – runs daily at 9:00 AM IST
                _logger.LogInformation("🚀 Production mode active – email job scheduled daily at 9:00 AM IST.");

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                   {
                        // Convert UTC → IST
                        var indiaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, indiaTimeZone);
                    var nextRun = new DateTime(now.Year, now.Month, now.Day, 9, 0, 0);

                    // If it's already past 9 AM today, schedule for tomorrow
                    if (now >= nextRun)
                            nextRun = nextRun.AddDays(1);

                        var delay = nextRun - now;
                        _logger.LogInformation("⏳ Waiting {Minutes:F1} minutes until next run at {NextRun}", delay.TotalMinutes, nextRun);

                        await Task.Delay(delay, stoppingToken);

                        await SafeRunJobAsync();

                        // Small buffer to avoid double trigger
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    catch (TaskCanceledException)
                    {
                        // graceful stop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ Unexpected error in DailyEmailScheduler loop");
                        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                    }
                }
            

            _logger.LogInformation("🛑 DailyEmailScheduler stopped at {Time}", DateTime.Now);
        }

        private async Task SafeRunJobAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var controller = ActivatorUtilities.CreateInstance<AdminController>(scope.ServiceProvider);

                _logger.LogInformation("📬 Running email job at {Time}", DateTime.Now);

                await controller.SendUpcomingCelebrationEmailsToMangerAsync();
                await controller.SendCelebrationEmailsEmpAsync();
                await controller.CheckRemainingLeavesAndNotifyAsync();
                await controller.NotifyManagerAboutEmployeesOnLeaveAsync();

                _logger.LogInformation("✅ Email job completed successfully at {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error occurred while running the email job");
            }
        }
    }
}
