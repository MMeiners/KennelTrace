namespace KennelTrace.Domain.Features.Tracing;

public enum TraceReasonCode
{
    SameLocation = 1,
    SameRoom = 2,
    Adjacent = 3,
    AirflowLinked = 4,
    TransportPathLinked = 5,
    ConnectedSpace = 6
}
