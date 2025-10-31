using LeaveManagement.Controllers;

namespace LeaveManagement
{
    public class DailyEmailScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public DailyEmailScheduler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                //var now = DateTime.Now;
                //testing  perpose date and time
                var now = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 9, 0, 0);
                var nextRun = DateTime.Today.AddHours(9); // 9:00 AM today

                // if it's already past 9 AM, schedule for tomorrow
                if (now > nextRun)
                    nextRun = nextRun.AddDays(1);

                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        // ✅ Manually create controller instance with DI dependencies
                        var controller = ActivatorUtilities.CreateInstance<AdminController>(scope.ServiceProvider);

                        await controller.SendUpcomingCelebrationEmailsToMangerAsync();
                        await controller.SendCelebrationEmailsEmpAsync();
                        await controller.CheckRemainingLeavesAndNotifyAsync();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error running daily email job: {ex.Message}");
                }

                // wait 24 hours until next run
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
