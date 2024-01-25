using Microsoft.OpenApi.Models;
using Prometheus;
using Rtl.News.RtlPoc.Application;
using Rtl.News.RtlPoc.Application.ExceptionHandlers;
using Rtl.News.RtlPoc.Infrastructure.Databases;
using Serilog;

namespace Rtl.News.RtlPoc.Api;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Host.UseSerilog((context, logger) => logger.ReadFrom.Configuration(context.Configuration));

        builder.Services.AddApplicationLayer(builder.Configuration);
        builder.Services.AddDatabaseInfrastructureLayer(builder.Configuration);
        builder.Services.AddDatabaseMigrations();

        // Register the mock dependencies
        builder.Services.Scan(scanner => scanner.FromAssemblies(typeof(Program).Assembly)
            .AddClasses(c => c.Where(type => type.Name.StartsWith("Mock")))
            .AsSelfWithInterfaces().WithSingletonLifetime());

        builder.Services.AddApplicationControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(swagger =>
        {
            swagger.CustomSchemaIds(type => type.FullName!["Rtl.News.RtlPoc.Contracts.".Length..]);

            swagger.SupportNonNullableReferenceTypes();
            swagger.SwaggerDoc("V1", new OpenApiInfo()
            {
                Title = "RtlPoc API",
                Description = """
				<p>This page documents the RtlPoc API.</p>
				""",
            });

            var apiDocumentationFilePath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            swagger.IncludeXmlComments(apiDocumentationFilePath);
            var contractsDocumentationFilePath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Contracts.Optional<object>).Assembly.GetName().Name}.xml");
            swagger.IncludeXmlComments(contractsDocumentationFilePath);
        });

        builder.Services.AddHealthChecks();

        var app = builder.Build();

        if (builder.Environment.IsDevelopment())
            app.UseDeveloperExceptionPage();

        app.UseExceptionHandler(app => app.Run(async context =>
            await context.RequestServices.GetRequiredService<RequestExceptionHandler>().HandleExceptionAsync()));

        app.UseRouting();

        // Expose a health check endpoint
        app.UseHealthChecks("/health");

        // Expose Prometheus metrics
        app.UseMetricServer();
        app.UseHttpMetrics();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseApplicationControllers();

        await app.RunAsync();
    }
}
