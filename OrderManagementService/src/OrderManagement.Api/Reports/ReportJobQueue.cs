using System.Threading.Channels;

namespace OrderManagement.Api.Reports
{
    public class ReportJobQueue
    {
        private readonly Channel<ReportJobRequest> _channel = Channel.CreateUnbounded<ReportJobRequest>();
        public ValueTask EnqueueAsync(ReportJobRequest request, CancellationToken cancellationToken = default)
        {
            return _channel.Writer.WriteAsync(request, cancellationToken);
        }

        public IAsyncEnumerable<ReportJobRequest> DequeueAllAsync(CancellationToken cancellationToken)
        {
            return _channel.Reader.ReadAllAsync(cancellationToken);
        }
    }
}
