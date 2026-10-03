using System.Collections.Generic;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Names of the roles that must exist for the application to work.
/// </summary>
public static class AppRoles
{
    /// <summary>Administers users and roles; grants no functional expense access.</summary>
    public const string Admin = "Admin";

    /// <summary>Creates, edits, submits and reads its own expenses.</summary>
    public const string Employee = "Employee";

    /// <summary>Approves or rejects submitted expenses that are not its own.</summary>
    public const string Approver = "Approver";

    /// <summary>Pays approved expenses that are not its own.</summary>
    public const string Finance = "Finance";

    /// <summary>Reads every expense and history, without write access.</summary>
    public const string Auditor = "Auditor";

    /// <summary>The roles that can read expenses, as a comma separated list for an authorization attribute.</summary>
    public const string ExpenseReaders = Employee + "," + Approver + "," + Finance + "," + Auditor;

    /// <summary>Gets every role the application needs.</summary>
    public static IReadOnlyList<string> All { get; } = new[] { Admin, Employee, Approver, Finance, Auditor };
}
