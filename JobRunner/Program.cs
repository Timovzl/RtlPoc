using Rtl.News.RtlPoc.Application;
using Rtl.News.RtlPoc.Infrastructure.Databases;
using Rtl.News.RtlPoc.JobRunner.Filters;
using Rtl.News.RtlPoc.JobRunner.Jobs;
using Hangfire;
using Hangfire.Prometheus.NetCore;
using Prometheus;
using Serilog;

namespace Rtl.News.RtlPoc.JobRunner;

public static class Program
{
	public static async Task Main(string[] args)
	{
		var builder = WebApplication.CreateBuilder(args);

		builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

		builder.Services.AddApplicationLayer(builder.Configuration);
		builder.Services.AddDatabaseInfrastructureLayer(builder.Configuration);
		builder.Services.AddDatabaseMigrations();

		// Register the current project's dependencies
		// Jobs must only be registered as IJob, as Hangfire will otherwise overlook attributes applied there
		// Other dependencies implemented by the current project should also only be resolved through their interfaces
		builder.Services.Scan(scanner => scanner.FromAssemblies(typeof(Job).Assembly)
			.AddClasses(c => c.Where(type => !type.Name.Contains('<') && !type.IsNested), publicOnly: false)
			.AsImplementedInterfaces().WithSingletonLifetime());

		builder.Services.AddHangfire(options => options.ConfigureHangfire());
		builder.Services.AddHangfireServer(options => options.ServerName = "RtlPoc.JobRunner");

		builder.Services.AddAuthentication();

		builder.Services.AddHealthChecks();

		var app = builder.Build();

		if (builder.Environment.IsDevelopment())
			app.UseDeveloperExceptionPage();

		app.UseRouting();

		// Expose a health check endpoint
		app.UseHealthChecks("/health");

		// Expose Prometheus metrics
		app.MapMetrics();
		app.UsePrometheusHangfireExporter();

		// Internally expose a dashboard for Hangfire
		app.MapHangfireDashboard(
			"/jobs",
			new DashboardOptions()
			{
				Authorization = [new HangfireNoAuthorizationFilter()],
				DashboardTitle = "RtlPoc Jobs",
				DisplayStorageConnectionString = false,
			});

		await app.RunAsync();
	}
}
