using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Infrastructure.Extensions;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.AddOptions<ArchiveSettings>()
    .Bind(builder.Configuration.GetSection(nameof(ArchiveSettings)))
    .ValidateDataAnnotations()
    .ValidateOnStart();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Logging.ClearProviders();

builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddWindowsService();

builder.Services.AddArchiveIndexer();

await builder.Build().RunAsync();

