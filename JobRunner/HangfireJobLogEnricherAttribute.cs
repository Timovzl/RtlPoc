using System.Runtime.CompilerServices;
using Hangfire.Common;
using Hangfire.Server;

namespace Rtl.News.RtlPoc.JobRunner;

/// <summary>
/// Adds job metadata to the ambient <see cref="ILogger"/> scope for the duration of <see cref="Hangfire"/> job runs.
/// </summary>
internal sealed class HangfireJobLogEnricherAttribute(
    ILogger<HangfireJobLogEnricherAttribute> logger)
    : JobFilterAttribute, IServerFilter
{
    private static readonly ConditionalWeakTable<string, IDisposable> LogScopes = [];

    public void OnPerforming(PerformingContext filterContext)
    {
        IDisposable? logScope = null;
        try
        {
            logScope = logger.BeginScope(new KeyValuePair<string, object>[]
            {
                KeyValuePair.Create("Job", (object)filterContext.BackgroundJob.Job.Type.Name),
                KeyValuePair.Create("JobRunId", (object)filterContext.BackgroundJob.Id),
            });

            if (logScope is not null)
                LogScopes.Add(filterContext.BackgroundJob.Id, logScope);
        }
        catch
        {
            logScope?.Dispose();
            throw;
        }
    }

    public void OnPerformed(PerformedContext filterContext)
    {
        if (LogScopes.TryGetValue(filterContext.BackgroundJob.Id, out var logScope))
        {
            logScope.Dispose();
            LogScopes.Remove(filterContext.BackgroundJob.Id);
        }
    }
}
