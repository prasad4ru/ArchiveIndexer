using ArchiveIndexer.Core.Configuration;
using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;


namespace ArchiveIndexer.Infrastructure.Catalog
{
    public sealed class ZipCatalog : IZipCatalog
    {
        private readonly string _catalogFile;

        private readonly Dictionary<string, ZipCatalogItem> _catalog = new(StringComparer.OrdinalIgnoreCase);

        private readonly ILogger<ZipCatalog> _logger;

        public ZipCatalog(IOptions<ArchiveSettings> options, ILogger<ZipCatalog> logger)
        {
            _logger = logger;
            _catalogFile = Path.Combine(options.Value.IndexPath, "ZipCatalog.json");
            Load();
        }

        public Task<IReadOnlyCollection<string>> GetAllZipPathsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyCollection<string> paths = _catalog.Keys.ToList();

            return Task.FromResult(paths);
        }

        public Task<bool> NeedsIndexingAsync(FileInfo file, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!_catalog.TryGetValue(file.FullName, out var item))
                return Task.FromResult(true);

            var changed = item.FileSize != file.Length || item.LastWriteTimeUtc != file.LastWriteTimeUtc;

            return Task.FromResult(changed);
        }

        public Task RemoveAsync(string zipPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_catalog.Remove(zipPath))
            {
                Save();
            }

            return Task.CompletedTask;
        }

        public Task UpdateAsync(FileInfo file, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _catalog[file.FullName] = new ZipCatalogItem
            {
                ZipPath = file.FullName,
                FileSize = file.Length,
                LastWriteTimeUtc = file.LastWriteTimeUtc,
                LastIndexedUtc = DateTime.UtcNow
            };

            Save();

            return Task.CompletedTask;
        }

        private void Load()
        {
            if (!File.Exists(_catalogFile))
                return;

            var json = File.ReadAllText(_catalogFile);

            var items = JsonSerializer.Deserialize<List<ZipCatalogItem>>(json);

            if (items == null)
                return;

            foreach (var item in items)
                _catalog[item.ZipPath] = item;

            _logger.LogInformation("Loaded {Count} ZIP catalog entries.", _catalog.Count);
        }

        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_catalogFile)!);

            var json = JsonSerializer.Serialize(_catalog.Values, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_catalogFile, json);
        }
    }
}
