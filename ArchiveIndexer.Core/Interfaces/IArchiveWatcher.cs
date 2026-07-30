namespace ArchiveIndexer.Core.Interfaces
{
    public interface IArchiveWatcher : IDisposable
    {
        void Start();

        void Stop();
    }
}
