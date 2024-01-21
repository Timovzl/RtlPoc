using Hangfire;

namespace Rtl.News.RtlPoc.JobRunner.Jobs;

public interface IJob
{
	string CronSchedule { get; }

	[DisableConcurrentExecution(timeoutInSeconds: 5 * 60)]
	Task Execute(CancellationToken cancellationToken);
}
