namespace ArchiveIndexer.Tests.TestSupport;

/// <summary>
/// Creates a unique temp folder on construction and recursively deletes it on
/// Dispose. Used by any test that needs a real on-disk Lucene index, ZIP file,
/// or catalog file - several of the classes under test (LuceneIndexer,
/// LuceneSearcher, ZipCatalog, ArchiveScanner, ArchiveWatcher) talk to
/// FSDirectory/File I/O directly rather than through an abstraction, so a real
/// temp folder is the most faithful way to exercise them.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ArchiveIndexerTests_" + Guid.NewGuid());
        Directory.CreateDirectory(Path);
    }

    public string GetSubPath(string relative) => System.IO.Path.Combine(Path, relative);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup - a lingering file lock (e.g. an undisposed Lucene
            // IndexWriter/SearcherManager in a failed test) shouldn't fail the run.
        }
    }
}
