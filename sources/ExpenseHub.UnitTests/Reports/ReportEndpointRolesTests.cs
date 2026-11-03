using System;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the first barrier of the report routes: the roles each route asks for. Requesting asks for the Employee or the
/// Auditor role (the rule of the scope is decided in the service), while listing, reading and downloading ask for nothing but
/// an authenticated user, because they are limited to the requests of the caller by ownership. They read the attributes by
/// reflection because the unit tests have no host to send requests to; the order of the answers on the running application
/// (the role before the validation of the body) is proved by the manual evidence in the CHECKPOINT-3.
/// </summary>
[TestClass]
public sealed class ReportEndpointRolesTests
{
    /// <summary>The route that requests a report asks for exactly the Employee and the Auditor roles, in one attribute of its own.</summary>
    [TestMethod]
    public void RequestRoute_AsksForExactlyEmployeeOrAuditor()
    {
        MethodInfo method = typeof(ReportsController).GetMethod(nameof(ReportsController.RequestAsync))!;

        AuthorizeAttribute[] attributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();

        Assert.HasCount(1, attributes);
        Assert.AreEqual("Auditor,Employee", Normalize(attributes[0].Roles));
        Assert.IsNull(attributes[0].Policy);
    }

    /// <summary>The three routes that read ask for no role and no policy: the only requirement is the authenticated user the controller already asks for, and the limit is ownership.</summary>
    /// <param name="action">The name of the action.</param>
    [TestMethod]
    [DataRow(nameof(ReportsController.ListAsync))]
    [DataRow(nameof(ReportsController.GetAsync))]
    [DataRow(nameof(ReportsController.ExcelAsync))]
    public void ReadRoutes_AskForNoRoleAndNoPolicy(string action)
    {
        MethodInfo method = typeof(ReportsController).GetMethod(action)!;

        Assert.IsEmpty(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray());

        AuthorizeAttribute[] controllerLevel = typeof(ReportsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
        Assert.HasCount(1, controllerLevel);
        Assert.IsNull(controllerLevel[0].Roles);
        Assert.IsNull(controllerLevel[0].Policy);
        Assert.IsNull(controllerLevel[0].AuthenticationSchemes);
    }

    /// <summary>The attribute of the controller asks only for an authenticated user, so no route gets a role by accident.</summary>
    [TestMethod]
    public void Controller_AsksOnlyForAnAuthenticatedUser()
    {
        AuthorizeAttribute attribute = typeof(ReportsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Single();

        Assert.IsNull(attribute.Roles);
        Assert.IsNull(attribute.Policy);
    }

    /// <summary>The only route with an extra requirement is the one that requests a report.</summary>
    [TestMethod]
    public void OnlyTheRequestRoute_HasARoleAttribute()
    {
        string[] withRoles = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(action => action.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
            .Select(action => action.Name)
            .ToArray();

        CollectionAssert.AreEqual(new[] { nameof(ReportsController.RequestAsync) }, withRoles);
    }

    private static string Normalize(string? roles)
    {
        string[] names = (roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(',', names.Order(StringComparer.Ordinal));
    }
}
