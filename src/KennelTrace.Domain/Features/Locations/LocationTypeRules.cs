namespace KennelTrace.Domain.Features.Locations;

public static class LocationTypeRules
{
    public static bool IsRoomLike(LocationType locationType) =>
        locationType is LocationType.Room or LocationType.Medical or LocationType.Isolation or LocationType.Intake;

    public static bool CanAppearAtFacilityRoot(LocationType locationType) =>
        locationType != LocationType.Kennel;

    public static bool RequiresContainmentParent(LocationType locationType) =>
        locationType == LocationType.Kennel;

    public static bool CanBeContainmentParent(LocationType locationType) =>
        locationType is LocationType.Room or LocationType.Medical or LocationType.Isolation or LocationType.Intake;

    public static bool IsNonKennelSpace(LocationType locationType) =>
        locationType != LocationType.Kennel;

    public static bool CanContainChild(LocationType parentType, LocationType childType) =>
        parentType switch
        {
            LocationType.Room or LocationType.Medical or LocationType.Isolation or LocationType.Intake =>
                childType == LocationType.Kennel || IsRoomLike(childType) || childType == LocationType.Other,
            _ => false
        };

    public static bool CanDirectlyHostAnimalStays(LocationType locationType) =>
        locationType is
            LocationType.Kennel or
            LocationType.Medical or
            LocationType.Isolation or
            LocationType.Intake or
            LocationType.Yard or
            LocationType.Other;
}
