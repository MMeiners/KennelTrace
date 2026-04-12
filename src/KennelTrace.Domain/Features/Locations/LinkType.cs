namespace KennelTrace.Domain.Features.Locations;

public enum LinkType
{
    AdjacentLeft = 1,
    AdjacentRight = 2,
    AdjacentAbove = 3,
    AdjacentBelow = 4,
    AdjacentOther = 5,
    Connected = 6,
    Airflow = 7,
    TransportPath = 8
}
