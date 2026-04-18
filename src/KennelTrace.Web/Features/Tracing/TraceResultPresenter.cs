using System.Globalization;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Web.Features.Tracing;

internal static class TraceResultPresenter
{
    public static string GetReasonLabel(TraceReasonCode reasonCode) =>
        reasonCode switch
        {
            TraceReasonCode.SameLocation => "Same location",
            TraceReasonCode.SameRoom => "Same room",
            TraceReasonCode.Adjacent => "Adjacent",
            TraceReasonCode.AirflowLinked => "Airflow linked",
            TraceReasonCode.TransportPathLinked => "Transport path",
            TraceReasonCode.ConnectedSpace => "Connected space",
            _ => reasonCode.ToString()
        };

    public static string GetLinkLabel(LinkType? linkType) =>
        linkType switch
        {
            null => "No link traversal",
            LinkType.AdjacentLeft => "Adjacent left",
            LinkType.AdjacentRight => "Adjacent right",
            LinkType.AdjacentAbove => "Adjacent above",
            LinkType.AdjacentBelow => "Adjacent below",
            LinkType.AdjacentOther => "Adjacent other",
            LinkType.Connected => "Connected",
            LinkType.Airflow => "Airflow",
            LinkType.TransportPath => "Transport path",
            _ => linkType.Value.ToString()
        };

    public static string GetMatchLabel(ImpactedLocationMatchKind matchKind) =>
        matchKind switch
        {
            ImpactedLocationMatchKind.ExactLocation => "Exact location",
            ImpactedLocationMatchKind.ScopedLocation => "Scoped descendant",
            _ => matchKind.ToString()
        };

    public static string FormatUtc(DateTime value) =>
        value.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public static string FormatUtc(DateTime? value) =>
        value is null ? "Open stay" : FormatUtc(value.Value);
}
