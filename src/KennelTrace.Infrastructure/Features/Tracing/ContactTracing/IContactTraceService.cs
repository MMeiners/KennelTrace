using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Infrastructure.Features.Tracing.ContactTracing;

public interface IContactTraceService
{
    Task<ContactTraceResult> RunAsync(ContactTraceRequest request, CancellationToken cancellationToken = default);
}
