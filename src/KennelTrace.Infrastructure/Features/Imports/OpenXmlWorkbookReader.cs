using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Infrastructure.Features.Imports;

public sealed class OpenXmlWorkbookReader : IWorkbookReader
{
    private static readonly XNamespace SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";

    private static readonly SheetSchema FacilitiesSchema = new(
        "Facilities",
        Required: false,
        Headers: ["FacilityCode", "FacilityName", "TimeZoneId", "IsActive", "Notes"]);

    private static readonly SheetSchema RoomsSchema = new(
        "Rooms",
        Required: true,
        Headers: ["FacilityCode", "RoomCode", "RoomName", "RoomType", "ParentLocationCode", "IsActive", "DisplayOrder", "SourceReference", "Notes"]);

    private static readonly SheetSchema KennelsSchema = new(
        "Kennels",
        Required: true,
        Headers: ["FacilityCode", "RoomCode", "KennelCode", "KennelName", "GridRow", "GridColumn", "StackLevel", "DisplayOrder", "IsActive", "SourceReference", "Notes"]);

    private static readonly SheetSchema LocationLinksSchema = new(
        "LocationLinks",
        Required: true,
        Headers: ["FacilityCode", "FromLocationCode", "ToLocationCode", "LinkType", "CreateInverse", "SourceReference", "Notes"]);

    public Task<ImportWorkbook> ReadAsync(string workbookPath, ICollection<ImportValidationIssueRecord> issues, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        ArgumentNullException.ThrowIfNull(issues);

        using var archive = ZipFile.OpenRead(workbookPath);
        var workbookDocument = XDocument.Load(ReadEntry(archive, "xl/workbook.xml"));
        var relationshipsDocument = XDocument.Load(ReadEntry(archive, "xl/_rels/workbook.xml.rels"));

        var relationshipTargets = relationshipsDocument.Root!
            .Elements(PackageRelationshipNamespace + "Relationship")
            .ToDictionary(
                x => (string)x.Attribute("Id")!,
                x => NormalizeWorksheetTarget((string)x.Attribute("Target")!),
                StringComparer.OrdinalIgnoreCase);

        var sheets = workbookDocument.Root!
            .Element(SpreadsheetNamespace + "sheets")!
            .Elements(SpreadsheetNamespace + "sheet")
            .Select(sheet => new WorkbookSheet(
                Name: (string)sheet.Attribute("name")!,
                Target: relationshipTargets[(string)sheet.Attribute(RelationshipNamespace + "id")!]))
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        var facilities = ReadSheetRows(
            archive,
            sheets,
            FacilitiesSchema,
            issues,
            (sheetName, row) => new FacilityImportRow(
                RowNumber: row.RowNumber,
                FacilityCode: RequiredText(sheetName, row, "FacilityCode", issues),
                FacilityName: RequiredText(sheetName, row, "FacilityName", issues),
                TimeZoneId: RequiredText(sheetName, row, "TimeZoneId", issues),
                IsActive: ReadBoolean(sheetName, row, "IsActive", defaultValue: true, issues),
                Notes: OptionalText(row, "Notes")));

        var rooms = ReadSheetRows(
            archive,
            sheets,
            RoomsSchema,
            issues,
            (sheetName, row) => new RoomImportRow(
                RowNumber: row.RowNumber,
                FacilityCode: RequiredText(sheetName, row, "FacilityCode", issues),
                RoomCode: RequiredText(sheetName, row, "RoomCode", issues),
                RoomName: RequiredText(sheetName, row, "RoomName", issues),
                RoomType: ReadLocationType(sheetName, row, "RoomType", issues),
                ParentLocationCode: OptionalText(row, "ParentLocationCode"),
                IsActive: ReadBoolean(sheetName, row, "IsActive", defaultValue: true, issues),
                DisplayOrder: ReadNullableInt(sheetName, row, "DisplayOrder", issues),
                SourceReference: OptionalText(row, "SourceReference"),
                Notes: OptionalText(row, "Notes")));

        var kennels = ReadSheetRows(
            archive,
            sheets,
            KennelsSchema,
            issues,
            (sheetName, row) => new KennelImportRow(
                RowNumber: row.RowNumber,
                FacilityCode: RequiredText(sheetName, row, "FacilityCode", issues),
                RoomCode: RequiredText(sheetName, row, "RoomCode", issues),
                KennelCode: RequiredText(sheetName, row, "KennelCode", issues),
                KennelName: OptionalText(row, "KennelName") ?? RequiredText(sheetName, row, "KennelCode", issues),
                GridRow: ReadNullableInt(sheetName, row, "GridRow", issues),
                GridColumn: ReadNullableInt(sheetName, row, "GridColumn", issues),
                StackLevel: ReadNullableInt(sheetName, row, "StackLevel", issues) ?? 0,
                DisplayOrder: ReadNullableInt(sheetName, row, "DisplayOrder", issues),
                IsActive: ReadBoolean(sheetName, row, "IsActive", defaultValue: true, issues),
                SourceReference: OptionalText(row, "SourceReference"),
                Notes: OptionalText(row, "Notes")));

        var locationLinks = ReadSheetRows(
            archive,
            sheets,
            LocationLinksSchema,
            issues,
            (sheetName, row) => new LocationLinkImportRow(
                RowNumber: row.RowNumber,
                FacilityCode: RequiredText(sheetName, row, "FacilityCode", issues),
                FromLocationCode: RequiredText(sheetName, row, "FromLocationCode", issues),
                ToLocationCode: RequiredText(sheetName, row, "ToLocationCode", issues),
                LinkType: ReadLinkType(sheetName, row, "LinkType", issues),
                CreateInverse: ReadBoolean(sheetName, row, "CreateInverse", defaultValue: true, issues),
                SourceReference: OptionalText(row, "SourceReference"),
                Notes: OptionalText(row, "Notes")));

        return Task.FromResult<ImportWorkbook>(new ImportWorkbook(facilities, rooms, kennels, locationLinks));
    }

    private static IReadOnlyList<T> ReadSheetRows<T>(
        ZipArchive archive,
        IReadOnlyDictionary<string, WorkbookSheet> sheets,
        SheetSchema schema,
        ICollection<ImportValidationIssueRecord> issues,
        Func<string, WorksheetRow, T> mapRow)
    {
        if (!sheets.TryGetValue(schema.Name, out var sheet))
        {
            if (schema.Required)
            {
                issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, schema.Name, $"Required sheet '{schema.Name}' is missing."));
            }

            return [];
        }

        var worksheet = XDocument.Load(ReadEntry(archive, sheet.Target));
        var rows = worksheet.Root!
            .Element(SpreadsheetNamespace + "sheetData")!
            .Elements(SpreadsheetNamespace + "row")
            .Select(ReadWorksheetRow)
            .ToList();

        if (rows.Count == 0)
        {
            issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, schema.Name, $"Sheet '{schema.Name}' is empty."));
            return [];
        }

        if (!ValidateHeaders(schema, rows[0], issues))
        {
            return [];
        }

        return rows.Skip(1)
            .Select(row => ProjectRow(schema, row))
            .Where(HasAnyValue)
            .Select(row => mapRow(schema.Name, row))
            .ToList();
    }

    private static bool ValidateHeaders(SheetSchema schema, WorksheetRow headerRow, ICollection<ImportValidationIssueRecord> issues)
    {
        var isValid = true;

        for (var columnIndex = 0; columnIndex < schema.Headers.Count; columnIndex++)
        {
            var columnReference = ToColumnReference(columnIndex + 1);
            headerRow.Cells.TryGetValue(columnReference, out var actual);
            var expected = schema.Headers[columnIndex];

            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                continue;
            }

            issues.Add(new ImportValidationIssueRecord(
                ImportIssueSeverity.Error,
                schema.Name,
                $"Header mismatch at {schema.Name}.{columnReference}1. Expected '{expected}' but found '{actual ?? string.Empty}'."));
            isValid = false;
        }

        return isValid;
    }

    private static WorksheetRow ReadWorksheetRow(XElement rowElement)
    {
        var rowNumber = (int?)rowElement.Attribute("r") ?? 0;
        var cells = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
        {
            var reference = (string?)cell.Attribute("r") ?? string.Empty;
            var columnReference = GetColumnLetters(reference);
            cells[columnReference] = ReadCellValue(cell);
        }

        return new WorksheetRow(rowNumber, cells);
    }

    private static WorksheetRow ProjectRow(SheetSchema schema, WorksheetRow row)
    {
        var projectedCells = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var columnIndex = 0; columnIndex < schema.Headers.Count; columnIndex++)
        {
            var columnReference = ToColumnReference(columnIndex + 1);
            row.Cells.TryGetValue(columnReference, out var value);
            projectedCells[schema.Headers[columnIndex]] = value ?? string.Empty;
        }

        return new WorksheetRow(row.RowNumber, projectedCells);
    }

    private static string ReadCellValue(XElement cell)
    {
        var type = (string?)cell.Attribute("t");
        return type switch
        {
            "inlineStr" => string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(x => x.Value)),
            "b" => cell.Element(SpreadsheetNamespace + "v")?.Value switch
            {
                "1" => "TRUE",
                "0" => "FALSE",
                var other => other ?? string.Empty
            },
            _ => cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty
        };
    }

    private static Stream ReadEntry(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath)
                    ?? throw new InvalidOperationException($"Workbook entry '{entryPath}' was not found.");
        return entry.Open();
    }

    private static string RequiredText(string sheetName, WorksheetRow row, string columnName, ICollection<ImportValidationIssueRecord> issues)
    {
        var value = OptionalText(row, columnName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, sheetName, $"Column '{columnName}' is required.", row.RowNumber));
        return string.Empty;
    }

    private static string? OptionalText(WorksheetRow row, string columnName)
    {
        return row.Cells.TryGetValue(columnName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static bool ReadBoolean(string sheetName, WorksheetRow row, string columnName, bool defaultValue, ICollection<ImportValidationIssueRecord> issues)
    {
        var value = OptionalText(row, columnName);
        if (value is null)
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, sheetName, $"Column '{columnName}' must be TRUE or FALSE, but found '{value}'.", row.RowNumber));
        return defaultValue;
    }

    private static int? ReadNullableInt(string sheetName, WorksheetRow row, string columnName, ICollection<ImportValidationIssueRecord> issues)
    {
        var value = OptionalText(row, columnName);
        if (value is null)
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, sheetName, $"Column '{columnName}' must be an integer, but found '{value}'.", row.RowNumber));
        return null;
    }

    private static LocationType ReadLocationType(string sheetName, WorksheetRow row, string columnName, ICollection<ImportValidationIssueRecord> issues)
    {
        var value = OptionalText(row, columnName);
        if (value is not null && Enum.TryParse<LocationType>(value, ignoreCase: false, out var locationType) && locationType != LocationType.Kennel)
        {
            return locationType;
        }

        issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, sheetName, $"Column '{columnName}' must be one of Room, Hallway, Medical, Isolation, Intake, Yard, or Other, but found '{value ?? string.Empty}'.", row.RowNumber));
        return LocationType.Other;
    }

    private static LinkType ReadLinkType(string sheetName, WorksheetRow row, string columnName, ICollection<ImportValidationIssueRecord> issues)
    {
        var value = OptionalText(row, columnName);
        if (value is not null && Enum.TryParse<LinkType>(value, ignoreCase: false, out var linkType))
        {
            return linkType;
        }

        issues.Add(new ImportValidationIssueRecord(ImportIssueSeverity.Error, sheetName, $"Column '{columnName}' must be one of AdjacentLeft, AdjacentRight, AdjacentAbove, AdjacentBelow, AdjacentOther, Connected, Airflow, or TransportPath, but found '{value ?? string.Empty}'.", row.RowNumber));
        return LinkType.Connected;
    }

    private static bool HasAnyValue(WorksheetRow row) => row.Cells.Values.Any(x => !string.IsNullOrWhiteSpace(x));

    private static string NormalizeWorksheetTarget(string target) =>
        target.TrimStart('/').Replace('\\', '/');

    private static string GetColumnLetters(string reference)
    {
        var letters = reference.TakeWhile(char.IsLetter).ToArray();
        return letters.Length == 0 ? string.Empty : new string(letters);
    }

    private static string ToColumnReference(int columnNumber)
    {
        var result = string.Empty;
        var current = columnNumber;

        while (current > 0)
        {
            current--;
            result = (char)('A' + (current % 26)) + result;
            current /= 26;
        }

        return result;
    }

    private sealed record WorkbookSheet(string Name, string Target);

    private sealed record SheetSchema(string Name, bool Required, IReadOnlyList<string> Headers);

    private sealed record WorksheetRow(int RowNumber, IReadOnlyDictionary<string, string> Cells);
}
