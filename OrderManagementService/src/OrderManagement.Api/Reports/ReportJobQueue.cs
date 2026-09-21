using System.Threading.Channels;

namespace OrderManagement.Api.Reports
{
    public class ReportJobQueue
    {
        // Bounded so a burst of report requests can't grow memory without limit
        private const int MaxQueueDepth = 100;
        private readonly Channel<ReportJobRequest> _channel = Channel.CreateBounded<ReportJobRequest>(MaxQueueDepth);

        // Non-blocking - returns false immediately if the queue is at capacity
        public bool TryEnqueue(ReportJobRequest request)
        {
            return _channel.Writer.TryWrite(request);
        }

        public IAsyncEnumerable<ReportJobRequest> DequeueAllAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
