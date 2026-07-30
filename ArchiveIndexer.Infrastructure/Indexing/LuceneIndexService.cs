using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace ArchiveIndexer.Infrastructure.Indexing
{
    public sealed class LuceneIndexService : BackgroundService, ILuceneIndexService
    {
        private const int CommitAfterDocuments = 5000;
        private const int DefaultCommitIntervalSeconds = 30;

        private readonly IDocumentQueue _queue;
        private readonly IArchiveIndexer _indexer;
        private readonly ArchiveSettings _settings;
        private readonly ILogger<LuceneIndexService> _logger;
        private readonly SemaphoreSlim _commitLock = new(1, 1);

        private int _uncommittedCount;

        public LuceneIndexService(IDocumentQueue queue, IArchiveIndexer indexer, IOptions<ArchiveSettings> options, ILogger<LuceneIndexService> logger)
        {
            _queue = queue;
            _indexer = indexer;
            _settings = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Lucene Index Service Started");

            // Previously ArchiveSettings.CommitIntervalSeconds was never read anywhere,
            // so commits only happened after 5000 documents or at shutdown. Any
            // consumer reading the index directly (e.g. a UI opening its own
            // IndexReader) would see stale data indefinitely between bursts.
            // This periodic commit closes that gap.
            var intervalSeconds = _settings.CommitIntervalSeconds > 0
                ? _settings.CommitIntervalSeconds
                : DefaultCommitIntervalSeconds;

            var consumeTask = ConsumeAsync(stoppingToken);
            var periodicCommitTask = RunPeriodicCommitAsync(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);

            await Task.WhenAll(consumeTask, periodicCommitTask);

            // Final flush of anything indexed but not yet committed.
            await CommitIfPendingAsync(CancellationToken.None, isFinal: true);
        }

        private async Task ConsumeAsync(CancellationToken stoppingToken)
        {
            await foreach (var document in _queue.ReadAllAsync(stoppingToken))
            {
                _logger.LogInformation("Indexing {File}", document.FileName);

                await _indexer.IndexAsync(document, stoppingToken);

                var newCount = Interlocked.Increment(ref _uncommittedCount);

                if (newCount >= CommitAfterDocuments)
                {
                    await CommitIfPendingAsync(stoppingToken, isFinal: false);
                }
            }
        }

        private async Task RunPeriodicCommitAsync(TimeSpan interval, CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(interval);

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CommitIfPendingAsync(stoppingToken, isFinal: false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown.
            }
        }

        private async Task CommitIfPendingAsync(CancellationToken token, bool isFinal)
        {
            await _commitLock.WaitAsync(CancellationToken.None);

            try
            {
                var pending = Interlocked.Exchange(ref _uncommittedCount, 0);

                if (pending == 0)
                    return;

                await _indexer.CommitAsync(token);

                _logger.LogInformation(
                    isFinal ? "Final Lucene commit completed. {Count} documents committed." : "Lucene commit completed. {Count} documents committed.",
                    pending);
            }
            finally
            {
                _commitLock.Release();
            }
        }

        // Optional if you still want to expose ILuceneIndexService
        public Task RunAsync(CancellationToken cancellationToken)
            => ExecuteAsync(cancellationToken);
    }



}
