using System;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the first barrier of the report routes: every route asks for an authenticated user and none is open to anonymous
/// requests. They read the attributes by reflection because the unit tests have no host to send requests to. Which roles the
/// request route asks for is checked in <see cref="ReportEndpointRolesTests"/>, and which role may ask for which scope in
/// <see cref="ReportAccessTests"/>.
/// </summary>
[TestClass]
public sealed class ReportEndpointAuthenticationTests
{
    /// <summary>The controller asks for an authenticated user in one single authorization attribute.</summary>
    [TestMethod]
    public void ReportsController_AsksForAnAuthenticatedUser()
    {
        AuthorizeAttribute[] attributes = typeof(ReportsController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();

        Assert.HasCount(1, attributes);
    }

    /// <summary>No action of the controller, and not the controller itself, is open to anonymous requests.</summary>
    [TestMethod]
    public void ReportsController_AllowsNoAnonymousAccess()
    {
        Assert.IsFalse(typeof(ReportsController).GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any());

        foreach (MethodInfo action in typeof(ReportsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.IsFalse(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any(), action.Name);
        }
    }

    /// <summary>The controller has exactly the four routes of the contract: request, list, detail and Excel.</summary>
    [TestMethod]
    public void ReportsController_HasExactlyTheFourRoutesOfTheContract()
    {
        string[] routes = typeof(ReportsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(action => action.GetCustomAttributes<HttpMethodAttribute>().Single())
            .Select(attribute => $"{attribute.HttpMethods.Single()} {attribute.Template}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["GET ", "GET {id:guid}", "GET {id:guid}/excel", "POST "];
        CollectionAssert.AreEqual(expected, routes);
        Assert.AreEqual("api/reports", typeof(ReportsController).GetCustomAttribute<RouteAttribute>()!.Template);
    }
}
