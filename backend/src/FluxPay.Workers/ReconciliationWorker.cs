using FluxPay.Core.Services;
using Microsoft.Extensions.Logging;

namespace FluxPay.Workers;

public class ReconciliationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReconciliationWorker> _logger;
    private readonly TimeSpan _scheduledTime = new(2, 0, 0);

    public ReconciliationWorker(
        IServiceProvider serviceProvider,
        ILogger<ReconciliationWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReconciliationWorker started. Scheduled to run daily at 2 AM UTC");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var nextRun = CalculateNextRunTime(now);
            var delay = nextRun - now;

            _logger.LogInformation("Next reconciliation scheduled for {NextRun} UTC (in {Delay})", nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);

                if (!stoppingToken.IsCancellationRequested)
                {
                    await RunReconciliationAsync(stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("ReconciliationWorker is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReconciliationWorker scheduling loop");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("ReconciliationWorker stopped");
    }

    private DateTime CalculateNextRunTime(DateTime now)
    {
        var today = now.Date;
        var scheduledToday = today.Add(_scheduledTime);

        if (now < scheduledToday)
        {
            return scheduledToday;
        }

        return today.AddDays(1).Add(_scheduledTime);
    }

    private async Task RunReconciliationAsync(CancellationToken stoppingToken)
    {
        var previousDay = DateTime.UtcNow.Date.AddDays(-1);

        _logger.LogInformation("Starting reconciliation for date {Date}", previousDay);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var reconciliationService = scope.ServiceProvider.GetRequiredService<IReconciliationService>();

            var report = await reconciliationService.ReconcileAsync(previousDay);

            _logger.LogInformation(
                "Reconciliation completed for {Date}. Total: {Total}, Matched: {Matched}, Mismatched: {Mismatched}",
                previousDay,
                report.TotalPayments,
                report.MatchedPayments,
                report.MismatchedPayments);

            if (report.MismatchedPayments > 0)
            {
                _logger.LogWarning(
                    "Reconciliation found {Count} mismatches for {Date}",
                    report.MismatchedPayments,
                    previousDay);

                foreach (var mismatch in report.Mismatches)
                {
                    _logger.LogWarning(
                        "Mismatch: Payment {PaymentId}, Type: {Type}, FluxPay: {FluxPayStatus}/{FluxPayAmount}, Provider: {ProviderStatus}/{ProviderAmount}",
                        mismatch.PaymentId,
                        mismatch.MismatchType,
                        mismatch.FluxPayStatus,
                        mismatch.FluxPayAmount,
                        mismatch.ProviderStatus,
                        mismatch.ProviderAmount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running reconciliation for date {Date}", previousDay);
        }
    }
}
