using ArchiveIndexer.Core.Interfaces;
using ArchiveIndexer.Core.Models;
using System.Threading.Channels;

namespace ArchiveIndexer.Infrastructure.Queue
{
    public sealed class DocumentQueue : IDocumentQueue
    {
        private readonly Channel<ArchiveDocument> _channel;

        public DocumentQueue()
        {
            _channel = Channel.CreateBounded<ArchiveDocument>(new BoundedChannelOptions(100000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public ValueTask EnqueueAsync(ArchiveDocument document, CancellationToken token)
        {
            return _channel.Writer.WriteAsync(document, token);
        }

        public IAsyncEnumerable<ArchiveDocument> ReadAllAsync(CancellationToken token)
        {
            return _channel.Reader.ReadAllAsync(token);
        }
    }
}
