using System.Data.Common;
using Hangfire;
using Rtl.News.RtlPoc.JobRunner.Jobs;

namespace Rtl.News.RtlPoc.JobRunner;

/// <summary>
/// Performs startup and/or shutdown logic for running a Hangfire <em>server</em>, i.e. an instance actually running jobs.
/// </summary>
internal sealed class HangfireServerHostedService(
    IEnumerable<IJob> jobs,
    IHostApplicationLifetime applicationLifetime,
    ILogger<HangfireServerHostedService> logger)
    : IHostedService
{
    private static readonly RecurringJobOptions JobOptions = new RecurringJobOptions() { TimeZone = TimeZoneInfo.Local };

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var job in jobs)
            {
                // Hangfire injects a cancellation token that honors job termination and application shutdown
                // If application shutdown terminates jobs, Hangfire re-runs them as soon as possible
                RecurringJob.AddOrUpdate(
                    recurringJobId: job.GetType().Name,
                    () => job.Execute(default),
                    job.CronSchedule,
                    JobOptions);

                // Note that removed jobs need to be manually removed using the Hangfire dashboard, in all environments
            }
        }
        catch (DbException e) when (e.Message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
        {
            // On the first run, MapHangfireDashboard() tries to access tables before hosted services have run and created the database (during local development)
            // Tracking issue: https://github.com/HangfireIO/Hangfire/issues/2139
            logger.LogCritical("MapHangfireDashboard() has attempted to create its tables before the database was created and so broken Hangfire's internal state. Terminating. Restart the application to resolve the issue.");
            applicationLifetime.StopApplication();
            cancellationToken.ThrowIfCancellationRequested();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
