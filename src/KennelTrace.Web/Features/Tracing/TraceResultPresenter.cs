using System.Globalization;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Web.Features.Tracing;

internal static class TraceResultPresenter
{
    public static string GetReasonLabel(TraceReasonCode reasonCode) =>
        GetReasonDefinition(reasonCode).Label;

    public static string GetLocationReasonExplanation(
        ImpactedLocationReason reason,
        string impactedLocationLabel,
        string sourceLocationLabel,
        string? scopeLocationLabel = null) =>
        GetReasonDefinition(reason.ReasonCode).GetExplanation(
            reason,
            impactedLocationLabel,
            sourceLocationLabel,
            scopeLocationLabel,
            TraceExplainabilitySubject.Location);

    public static string GetAnimalReasonExplanation(
        ImpactedLocationReason reason,
        string impactedLocationLabel,
        string sourceLocationLabel,
        string? scopeLocationLabel = null) =>
        GetReasonDefinition(reason.ReasonCode).GetExplanation(
            reason,
            impactedLocationLabel,
            sourceLocationLabel,
            scopeLocationLabel,
            TraceExplainabilitySubject.Animal);

    public static string GetFallbackLocationReasonExplanation(TraceReasonCode reasonCode, string impactedLocationLabel) =>
        GetReasonDefinition(reasonCode).GetFallbackExplanation(impactedLocationLabel, TraceExplainabilitySubject.Location);

    public static string GetFallbackAnimalReasonExplanation(TraceReasonCode reasonCode, string impactedLocationLabel) =>
        GetReasonDefinition(reasonCode).GetFallbackExplanation(impactedLocationLabel, TraceExplainabilitySubject.Animal);

    public static string GetLinkLabel(LinkType? linkType) =>
        linkType switch
        {
            null => "No stored link traversal",
            LinkType.AdjacentLeft => "Adjacent left",
            LinkType.AdjacentRight => "Adjacent right",
            LinkType.AdjacentAbove => "Adjacent above",
            LinkType.AdjacentBelow => "Adjacent below",
            LinkType.AdjacentOther => "Adjacent nearby",
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

    private static TraceReasonDefinition GetReasonDefinition(TraceReasonCode reasonCode) =>
        reasonCode switch
        {
            TraceReasonCode.SameLocation => new(
                "Same location",
                static (reason, impactedLocationLabel, sourceLocationLabel, _, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in {impactedLocationLabel}, the same location as the source stay at {sourceLocationLabel}."
                        : $"Included because {impactedLocationLabel} is the same location as the source stay at {sourceLocationLabel}.",
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal shared the same location during overlapping time in {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} matched the same location as the source stay."),

            TraceReasonCode.SameRoom => new(
                "Same room",
                static (reason, impactedLocationLabel, sourceLocationLabel, scopeLocationLabel, subject) =>
                {
                    if (reason.MatchKind == ImpactedLocationMatchKind.ScopedLocation
                        && !string.IsNullOrWhiteSpace(scopeLocationLabel))
                    {
                        return subject == TraceExplainabilitySubject.Animal
                            ? $"Included because this animal overlapped in {impactedLocationLabel}, inside {scopeLocationLabel}, the same room as the source location {sourceLocationLabel}."
                            : $"Included because {impactedLocationLabel} is inside {scopeLocationLabel}, the same room as the source location {sourceLocationLabel}.";
                    }

                    return subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in {impactedLocationLabel}, the same room as the source location {sourceLocationLabel}."
                        : $"Included because {impactedLocationLabel} is the same room as the source location {sourceLocationLabel}.";
                },
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in the same room at {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} was included through the same-room rule."),

            TraceReasonCode.Adjacent => new(
                "Adjacent",
                static (reason, impactedLocationLabel, sourceLocationLabel, _, subject) =>
                {
                    var adjacencyText = reason.TraversalDepth <= 1
                        ? $"{GetLinkLabel(reason.ViaLinkType).ToLowerInvariant()} to the source location {sourceLocationLabel}"
                        : $"{reason.TraversalDepth} authored adjacency steps from the source location {sourceLocationLabel}";

                    return subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in {impactedLocationLabel}, which is {adjacencyText}."
                        : $"Included because {impactedLocationLabel} is {adjacencyText}.";
                },
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in an explicitly adjacent location at {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} was included through an explicit adjacency link."),

            TraceReasonCode.AirflowLinked => new(
                "Airflow linked",
                static (reason, impactedLocationLabel, sourceLocationLabel, scopeLocationLabel, subject) =>
                    BuildTopologyExplanation(
                        impactedLocationLabel,
                        sourceLocationLabel,
                        scopeLocationLabel,
                        subject,
                        "an airflow-linked space",
                        reason.TraversalDepth),
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in an airflow-linked space at {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} was included through an airflow link."),

            TraceReasonCode.TransportPathLinked => new(
                "Transport path",
                static (reason, impactedLocationLabel, sourceLocationLabel, scopeLocationLabel, subject) =>
                    BuildTopologyExplanation(
                        impactedLocationLabel,
                        sourceLocationLabel,
                        scopeLocationLabel,
                        subject,
                        "a transport-path-linked space",
                        reason.TraversalDepth),
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in a transport-path-linked space at {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} was included through a transport-path link."),

            TraceReasonCode.ConnectedSpace => new(
                "Connected space",
                static (reason, impactedLocationLabel, sourceLocationLabel, scopeLocationLabel, subject) =>
                    BuildTopologyExplanation(
                        impactedLocationLabel,
                        sourceLocationLabel,
                        scopeLocationLabel,
                        subject,
                        "a connected space",
                        reason.TraversalDepth),
                static (impactedLocationLabel, subject) =>
                    subject == TraceExplainabilitySubject.Animal
                        ? $"Included because this animal overlapped in a connected space at {impactedLocationLabel}."
                        : $"Included because {impactedLocationLabel} was included through a connected-space link."),

            _ => throw new ArgumentOutOfRangeException(nameof(reasonCode), reasonCode, null)
        };

    private static string BuildTopologyExplanation(
        string impactedLocationLabel,
        string sourceLocationLabel,
        string? scopeLocationLabel,
        TraceExplainabilitySubject subject,
        string directRelationshipText,
        int traversalDepth)
    {
        var relationshipText = traversalDepth <= 1
            ? directRelationshipText
            : $"a space {traversalDepth} stored-link steps away";

        if (!string.IsNullOrWhiteSpace(scopeLocationLabel))
        {
            return subject == TraceExplainabilitySubject.Animal
                ? $"Included because this animal overlapped in {impactedLocationLabel}, inside {scopeLocationLabel}, {relationshipText} from the source location {sourceLocationLabel}."
                : $"Included because {impactedLocationLabel} is inside {scopeLocationLabel}, {relationshipText} from the source location {sourceLocationLabel}.";
        }

        return subject == TraceExplainabilitySubject.Animal
            ? $"Included because this animal overlapped in {impactedLocationLabel}, {relationshipText} from the source location {sourceLocationLabel}."
            : $"Included because {impactedLocationLabel} is {relationshipText} from the source location {sourceLocationLabel}.";
    }

    private readonly record struct TraceReasonDefinition(
        string Label,
        Func<ImpactedLocationReason, string, string, string?, TraceExplainabilitySubject, string> GetExplanation,
        Func<string, TraceExplainabilitySubject, string> GetFallbackExplanation);

    private enum TraceExplainabilitySubject
    {
        Location = 1,
        Animal = 2
    }
}
