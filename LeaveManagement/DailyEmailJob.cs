using LeaveManagement.Controllers;

namespace LeaveManagement.Jobs
{
    public class DailyEmailJob
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyEmailJob> _logger;

        public DailyEmailJob(IServiceProvider serviceProvider, ILogger<DailyEmailJob> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var controller = ActivatorUtilities.CreateInstance<AdminController>(scope.ServiceProvider);

                _logger.LogInformation("📬 Running daily email job at {Time}", DateTime.Now);

                //  Run these daily
                await controller.SendUpcomingCelebrationEmailsToMangerAsync();
                await controller.SendCelebrationEmailsEmpAsync();
                await controller.NotifyManagerAboutEmployeesOnLeaveAsync();

                //  Run this only on the 1st of each month
                if (DateTime.Now.Day == 1)
                {
                    _logger.LogInformation("Running monthly leave balance check (1st of month)");
                    await controller.CheckRemainingLeavesAndNotifyAsync();
                }

                _logger.LogInformation("Daily email job completed successfully at {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while running daily email job");
            }
        }
    }
}
