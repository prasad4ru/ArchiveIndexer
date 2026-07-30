using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ArchiveIndexer.Infrastructure.Watching
{
    public sealed class ArchiveScannerService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ArchiveScannerService> _logger;

        public ArchiveScannerService(IServiceScopeFactory scopeFactory, ILogger<ArchiveScannerService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("=======================================");
            _logger.LogInformation("Archive Scanner Service Started");
            _logger.LogInformation("=======================================");

            using var scope = _scopeFactory.CreateScope();

            var scanner = scope.ServiceProvider.GetRequiredService<IArchiveScanner>();
            var watcher = scope.ServiceProvider.GetRequiredService<IArchiveWatcher>();

            _logger.LogInformation("Starting initial archive scan...");

            await scanner.ScanAsync(stoppingToken);

            _logger.LogInformation("Initial archive scan completed.");

            watcher.Start();

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Archive Scanner Service stopping...");
            }
            finally
            {
                watcher.Stop();
            }
        }
    }
}
