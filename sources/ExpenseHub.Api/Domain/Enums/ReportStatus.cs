namespace ExpenseHub.Api.Domain.Enums;

/// <summary>
/// Progress of a report request. These states belong to the application, not to the broker.
/// </summary>
public enum ReportStatus
{
    /// <summary>Registered and waiting to be processed.</summary>
    Pending,

    /// <summary>Being processed.</summary>
    Processing,

    /// <summary>Processed, with the result saved. Final state.</summary>
    Completed,

    /// <summary>Could not be processed, with a sanitized error saved. Final state.</summary>
    Failed,
}
