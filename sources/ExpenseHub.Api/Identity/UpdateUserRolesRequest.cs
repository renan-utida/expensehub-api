using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ExpenseHub.Api.Identity;

/// <summary>
/// Data sent to <c>PUT /api/admin/users/{id}/roles</c>. The list replaces the roles of the user:
/// the roles listed are granted and the ones left out are removed.
/// </summary>
public sealed class UpdateUserRolesRequest
{
    /// <summary>Maximum number of role names in one request.</summary>
    public const int RolesMaxCount = 50;

    /// <summary>
    /// Gets the names of the roles the user must end up with. It is required, so an omitted list
    /// is a <c>400</c> and never silently removes every role. An empty list removes every role.
    /// </summary>
    [Required]
    [MaxLength(RolesMaxCount)]
    public IReadOnlyList<string>? Roles { get; init; }
}
