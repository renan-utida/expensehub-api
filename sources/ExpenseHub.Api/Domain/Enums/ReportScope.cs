namespace ExpenseHub.Api.Domain.Enums;

/// <summary>
/// Whose expenses a report adds up. The scope is registered when the report is requested and never changes.
/// </summary>
public enum ReportScope
{
    /// <summary>Only the expenses of the user who requested the report.</summary>
    Mine,

    /// <summary>The expenses of every user.</summary>
    Global,
}
