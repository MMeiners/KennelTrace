using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed record ImportBatchLogResult(
    long ImportBatchId,
    ImportBatchStatus Status);
