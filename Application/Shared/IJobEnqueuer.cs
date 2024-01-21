using Architect.AmbientContexts;

namespace Rtl.News.RtlPoc.Application.Shared;

/// <summary>
/// Supports enqueuing of one-off job runs.
/// </summary>
public interface IJobEnqueuer
{
	Task EnqueueJob(string jobNamePrefix);

	Task ScheduleJob(string jobNamePrefix, DateTimeOffset instant);

	Task ScheduleJob(string jobNamePrefix, TimeSpan delay)
	{
		var instant = new DateTimeOffset(Clock.UtcNow).Add(delay);
		return this.ScheduleJob(jobNamePrefix, instant);
	}
}
