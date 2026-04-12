using System.Text;
using KennelTrace.Domain.Features.Imports;

namespace KennelTrace.Infrastructure.Features.Imports;

public static class ImportReportFormatter
{
    public static string Format(string sourceFileName, ImportValidationReport report)
    {
        var builder = new StringBuilder();
        builder.Append("Validation ");
        builder.Append(report.IsValid ? "succeeded" : "failed");
        builder.Append(" for ");
        builder.Append(sourceFileName);
        builder.Append(". ");
        builder.Append(report.ErrorCount);
        builder.Append(" error(s), ");
        builder.Append(report.WarningCount);
        builder.Append(" warning(s).");

        foreach (var issue in report.Issues)
        {
            builder.AppendLine();
            builder.Append("- [");
            builder.Append(issue.Severity);
            builder.Append("] ");
            builder.Append(issue.SheetName);

            if (issue.RowNumber is not null)
            {
                builder.Append(" row ");
                builder.Append(issue.RowNumber.Value);
            }

            if (!string.IsNullOrWhiteSpace(issue.ItemKey))
            {
                builder.Append(" (");
                builder.Append(issue.ItemKey);
                builder.Append(')');
            }

            builder.Append(": ");
            builder.Append(issue.Message);
        }

        return builder.ToString();
    }
}
