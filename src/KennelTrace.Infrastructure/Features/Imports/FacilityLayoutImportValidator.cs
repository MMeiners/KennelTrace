using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed class FacilityLayoutImportValidator
{
    public void Validate(ImportWorkbook workbook, ICollection<ImportValidationIssueRecord> issues)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(issues);

        ValidateFacilities(workbook.Facilities, issues);

        var knownFacilities = workbook.Facilities
            .Select(x => x.FacilityCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateRooms(workbook.Rooms, knownFacilities, issues);
        ValidateKennels(workbook.Rooms, workbook.Kennels, knownFacilities, issues);
        ValidateLocationLinks(workbook.Rooms, workbook.Kennels, workbook.LocationLinks, knownFacilities, issues);
    }

    private static void ValidateFacilities(IReadOnlyList<FacilityImportRow> facilities, ICollection<ImportValidationIssueRecord> issues)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var facility in facilities)
        {
            if (!seen.Add(facility.FacilityCode))
            {
                issues.Add(Error("Facilities", facility.RowNumber, facility.FacilityCode, $"FacilityCode '{facility.FacilityCode}' is duplicated in this workbook."));
            }
        }
    }

    private static void ValidateRooms(
        IReadOnlyList<RoomImportRow> rooms,
        ISet<string> knownFacilities,
        ICollection<ImportValidationIssueRecord> issues)
    {
        var roomLookup = new Dictionary<string, RoomImportRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var room in rooms)
        {
            if (knownFacilities.Count > 0 && !knownFacilities.Contains(room.FacilityCode))
            {
                issues.Add(Error("Rooms", room.RowNumber, BuildLocationKey(room.FacilityCode, room.RoomCode), $"FacilityCode '{room.FacilityCode}' is not defined in the Facilities sheet."));
            }

            var key = BuildLocationKey(room.FacilityCode, room.RoomCode);
            if (!roomLookup.TryAdd(key, room))
            {
                issues.Add(Error("Rooms", room.RowNumber, key, $"RoomCode '{room.RoomCode}' is duplicated within facility '{room.FacilityCode}'."));
            }
        }

        foreach (var room in rooms)
        {
            if (string.IsNullOrWhiteSpace(room.ParentLocationCode))
            {
                continue;
            }

            var parentKey = BuildLocationKey(room.FacilityCode, room.ParentLocationCode);
            if (!roomLookup.ContainsKey(parentKey))
            {
                issues.Add(Error("Rooms", room.RowNumber, BuildLocationKey(room.FacilityCode, room.RoomCode), $"ParentLocationCode '{room.ParentLocationCode}' does not exist in facility '{room.FacilityCode}'."));
            }
        }

        foreach (var room in rooms)
        {
            if (HasCycle(room, roomLookup))
            {
                issues.Add(Error("Rooms", room.RowNumber, BuildLocationKey(room.FacilityCode, room.RoomCode), $"ParentLocationCode creates a cycle for room '{room.RoomCode}' in facility '{room.FacilityCode}'."));
            }
        }
    }

    private static void ValidateKennels(
        IReadOnlyList<RoomImportRow> rooms,
        IReadOnlyList<KennelImportRow> kennels,
        ISet<string> knownFacilities,
        ICollection<ImportValidationIssueRecord> issues)
    {
        var roomLookup = rooms
            .GroupBy(x => BuildLocationKey(x.FacilityCode, x.RoomCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var locationCodes = rooms
            .Select(x => BuildLocationKey(x.FacilityCode, x.RoomCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var occupiedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kennel in kennels)
        {
            var locationKey = BuildLocationKey(kennel.FacilityCode, kennel.KennelCode);

            if (knownFacilities.Count > 0 && !knownFacilities.Contains(kennel.FacilityCode))
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"FacilityCode '{kennel.FacilityCode}' is not defined in the Facilities sheet."));
            }

            var roomKey = BuildLocationKey(kennel.FacilityCode, kennel.RoomCode);
            if (!roomLookup.TryGetValue(roomKey, out var room))
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"RoomCode '{kennel.RoomCode}' does not exist in facility '{kennel.FacilityCode}'."));
            }
            else if (!LocationTypeRules.CanContainChild(room.RoomType, LocationType.Kennel))
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"RoomCode '{kennel.RoomCode}' is a '{room.RoomType}' location and cannot parent kennels in MVP."));
            }

            if (!locationCodes.Add(locationKey))
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"LocationCode '{kennel.KennelCode}' is duplicated within facility '{kennel.FacilityCode}'."));
            }

            if (kennel.GridRow is null && kennel.GridColumn is null)
            {
                if (kennel.IsActive)
                {
                    issues.Add(Warning("Kennels", kennel.RowNumber, locationKey, $"Kennel '{kennel.KennelCode}' has no grid placement. GridRow and GridColumn are both blank."));
                }

                continue;
            }

            if (kennel.GridRow is null || kennel.GridColumn is null)
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, "GridRow and GridColumn must both be populated or both be blank."));
                continue;
            }

            if (kennel.GridRow < 0)
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"GridRow '{kennel.GridRow}' must be zero or greater."));
            }

            if (kennel.GridColumn < 0)
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"GridColumn '{kennel.GridColumn}' must be zero or greater."));
            }

            if (kennel.StackLevel < 0)
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"StackLevel '{kennel.StackLevel}' must be zero or greater."));
            }

            var slotKey = $"{kennel.FacilityCode}/{kennel.RoomCode}/{kennel.GridRow}/{kennel.GridColumn}/{kennel.StackLevel}";
            if (!occupiedSlots.Add(slotKey))
            {
                issues.Add(Error("Kennels", kennel.RowNumber, locationKey, $"Grid position ({kennel.GridRow}, {kennel.GridColumn}, stack {kennel.StackLevel}) is already used in room '{kennel.RoomCode}' for facility '{kennel.FacilityCode}'."));
            }
        }

        var activeKennelRooms = kennels
            .Where(x => x.IsActive)
            .Select(x => BuildLocationKey(x.FacilityCode, x.RoomCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var room in rooms.Where(x => x.IsActive && (LocationTypeRules.IsRoomLike(x.RoomType) || x.RoomType is LocationType.Yard or LocationType.Other)))
        {
            if (!activeKennelRooms.Contains(BuildLocationKey(room.FacilityCode, room.RoomCode)))
            {
                issues.Add(Warning("Rooms", room.RowNumber, BuildLocationKey(room.FacilityCode, room.RoomCode), $"Location '{room.RoomCode}' has no active kennels in this workbook."));
            }
        }
    }

    private static void ValidateLocationLinks(
        IReadOnlyList<RoomImportRow> rooms,
        IReadOnlyList<KennelImportRow> kennels,
        IReadOnlyList<LocationLinkImportRow> links,
        ISet<string> knownFacilities,
        ICollection<ImportValidationIssueRecord> issues)
    {
        var roomLookup = rooms
            .GroupBy(x => BuildLocationKey(x.FacilityCode, x.RoomCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().RoomType, StringComparer.OrdinalIgnoreCase);
        var kennelLookup = kennels
            .GroupBy(x => BuildLocationKey(x.FacilityCode, x.KennelCode), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, _ => LocationType.Kennel, StringComparer.OrdinalIgnoreCase);
        var endpointFacilityLookup = rooms
            .Select(x => (Code: x.RoomCode, Facility: x.FacilityCode))
            .Concat(kennels.Select(x => (Code: x.KennelCode, Facility: x.FacilityCode)))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Facility).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        var directLinks = new Dictionary<string, LocationLinkImportRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            var itemKey = BuildLinkKey(link);

            if (knownFacilities.Count > 0 && !knownFacilities.Contains(link.FacilityCode))
            {
                issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"FacilityCode '{link.FacilityCode}' is not defined in the Facilities sheet."));
            }

            if (link.FromLocationCode.Equals(link.ToLocationCode, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"FromLocationCode and ToLocationCode cannot both be '{link.FromLocationCode}'."));
            }

            var fromFound = TryResolveLocationType(link.FacilityCode, link.FromLocationCode, roomLookup, kennelLookup, out var fromType);
            var toFound = TryResolveLocationType(link.FacilityCode, link.ToLocationCode, roomLookup, kennelLookup, out var toType);

            if (!fromFound)
            {
                issues.Add(Error("LocationLinks", link.RowNumber, itemKey, BuildMissingEndpointMessage("FromLocationCode", link.FacilityCode, link.FromLocationCode, endpointFacilityLookup)));
            }

            if (!toFound)
            {
                issues.Add(Error("LocationLinks", link.RowNumber, itemKey, BuildMissingEndpointMessage("ToLocationCode", link.FacilityCode, link.ToLocationCode, endpointFacilityLookup)));
            }

            if (fromFound && toFound)
            {
                if (LinkTypeRules.IsAdjacency(link.LinkType))
                {
                    if (fromType != LocationType.Kennel || toType != LocationType.Kennel)
                    {
                        issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"Adjacency link '{link.LinkType}' must connect kennel endpoints, but '{link.FromLocationCode}' is '{fromType}' and '{link.ToLocationCode}' is '{toType}'."));
                    }
                }
                else if (fromType == LocationType.Kennel || toType == LocationType.Kennel)
                {
                    issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"Topology link '{link.LinkType}' must connect room-like endpoints in MVP, but '{link.FromLocationCode}' is '{fromType}' and '{link.ToLocationCode}' is '{toType}'."));
                }
            }

            var directKey = itemKey;
            if (!directLinks.TryAdd(directKey, link))
            {
                issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"Directed link '{link.LinkType}' from '{link.FromLocationCode}' to '{link.ToLocationCode}' is duplicated within facility '{link.FacilityCode}'."));
                continue;
            }

            foreach (var reverse in directLinks.Values.Where(x =>
                         !ReferenceEquals(x, link) &&
                         x.FacilityCode.Equals(link.FacilityCode, StringComparison.OrdinalIgnoreCase) &&
                         x.FromLocationCode.Equals(link.ToLocationCode, StringComparison.OrdinalIgnoreCase) &&
                         x.ToLocationCode.Equals(link.FromLocationCode, StringComparison.OrdinalIgnoreCase)))
            {
                var expectedInverse = LinkTypeRules.InverseOf(reverse.LinkType);
                if (link.LinkType != expectedInverse)
                {
                    issues.Add(Error("LocationLinks", link.RowNumber, itemKey, $"Link '{reverse.FromLocationCode}' -> '{reverse.ToLocationCode}' uses '{reverse.LinkType}', so the inverse row must use '{expectedInverse}', not '{link.LinkType}'."));
                }
            }

            if (ContainsUncertainty(link))
            {
                issues.Add(Warning("LocationLinks", link.RowNumber, itemKey, "Link notes indicate uncertainty and should be reviewed before commit."));
            }
        }
    }

    private static bool ContainsUncertainty(LocationLinkImportRow row) =>
        ContainsKeyword(row.Notes, "uncertain") || ContainsKeyword(row.SourceReference, "handwritten");

    private static bool ContainsKeyword(string? value, string keyword) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(keyword, StringComparison.OrdinalIgnoreCase);

    private static bool TryResolveLocationType(
        string facilityCode,
        string locationCode,
        IReadOnlyDictionary<string, LocationType> roomLookup,
        IReadOnlyDictionary<string, LocationType> kennelLookup,
        out LocationType locationType)
    {
        var key = BuildLocationKey(facilityCode, locationCode);
        if (kennelLookup.TryGetValue(key, out locationType))
        {
            return true;
        }

        return roomLookup.TryGetValue(key, out locationType);
    }

    private static string BuildMissingEndpointMessage(
        string endpointName,
        string facilityCode,
        string locationCode,
        IReadOnlyDictionary<string, string[]> endpointFacilityLookup)
    {
        if (!endpointFacilityLookup.TryGetValue(locationCode, out var facilities))
        {
            return $"{endpointName} '{locationCode}' does not exist in facility '{facilityCode}'.";
        }

        return facilities.Length == 1
            ? $"{endpointName} '{locationCode}' exists in facility '{facilities[0]}', not in facility '{facilityCode}'. Cross-facility links are invalid in MVP."
            : $"{endpointName} '{locationCode}' does not resolve uniquely inside facility '{facilityCode}'.";
    }

    private static bool HasCycle(RoomImportRow room, IReadOnlyDictionary<string, RoomImportRow> roomLookup)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = room;

        while (!string.IsNullOrWhiteSpace(current.ParentLocationCode) &&
               roomLookup.TryGetValue(BuildLocationKey(current.FacilityCode, current.ParentLocationCode), out var parent))
        {
            if (!visited.Add(parent.RoomCode) || parent.RoomCode.Equals(room.RoomCode, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static ImportValidationIssueRecord Error(string sheetName, int? rowNumber, string? itemKey, string message) =>
        new(ImportIssueSeverity.Error, sheetName, message, rowNumber, itemKey);

    private static ImportValidationIssueRecord Warning(string sheetName, int? rowNumber, string? itemKey, string message) =>
        new(ImportIssueSeverity.Warning, sheetName, message, rowNumber, itemKey);

    private static string BuildLocationKey(string facilityCode, string locationCode) =>
        $"{facilityCode}/{locationCode}";

    private static string BuildLinkKey(LocationLinkImportRow row) =>
        $"{row.FacilityCode}/{row.FromLocationCode}->{row.ToLocationCode}/{row.LinkType}";
}
