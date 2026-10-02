using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the validation of the request that replaces the roles of a user.
/// </summary>
[TestClass]
public sealed class UpdateUserRolesRequestTests
{
    /// <summary>A missing list is rejected, so an omitted field can never silently remove every role.</summary>
    [TestMethod]
    public void Validate_MissingRoleList_ReportsRolesError()
    {
        var request = new UpdateUserRolesRequest();

        CollectionAssert.Contains(Validate(request), nameof(UpdateUserRolesRequest.Roles));
    }

    /// <summary>An empty list is valid: it means the user ends up with no roles.</summary>
    [TestMethod]
    public void Validate_EmptyRoleList_HasNoErrors()
    {
        var request = new UpdateUserRolesRequest { Roles = Array.Empty<string>() };

        Assert.IsEmpty(Validate(request));
    }

    /// <summary>A list of role names is valid at this level; whether they are known roles is a service rule.</summary>
    [TestMethod]
    public void Validate_ListOfNames_HasNoErrors()
    {
        var request = new UpdateUserRolesRequest { Roles = new[] { "Employee", "Approver" } };

        Assert.IsEmpty(Validate(request));
    }

    /// <summary>A list with more names than the allowed maximum is rejected.</summary>
    [TestMethod]
    public void Validate_TooManyNames_ReportsRolesError()
    {
        var request = new UpdateUserRolesRequest
        {
            Roles = Enumerable.Repeat("Employee", UpdateUserRolesRequest.RolesMaxCount + 1).ToList(),
        };

        CollectionAssert.Contains(Validate(request), nameof(UpdateUserRolesRequest.Roles));
    }

    private static List<string> Validate(UpdateUserRolesRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToList();
    }
}
