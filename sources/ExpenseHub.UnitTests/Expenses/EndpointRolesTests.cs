using System;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the first barrier of the authorization matrix: the roles each endpoint asks for. The decision on a given expense
/// (owner and state) is made in the service and is tested there; this only checks that no endpoint was opened to, or closed
/// to, the wrong roles. It reads the attributes by reflection because the unit tests have no host to send requests to; a
/// functional test would send the requests instead.
/// </summary>
[TestClass]
public sealed class EndpointRolesTests
{
    /// <summary>Each endpoint asks for exactly the roles the matrix gives to it, in one single authorization attribute.</summary>
    /// <param name="controller">The controller.</param>
    /// <param name="action">The action.</param>
    /// <param name="expectedRoles">The roles the matrix allows, separated by comma.</param>
    [TestMethod]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.CreateAsync), "Employee")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.UpdateAsync), "Employee")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.SubmitAsync), "Employee")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.ApproveAsync), "Approver")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.RejectAsync), "Approver")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.PayAsync), "Finance")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.ListAsync), "Employee,Approver,Finance,Auditor")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.GetAsync), "Employee,Approver,Finance,Auditor")]
    [DataRow(typeof(ExpensesController), nameof(ExpensesController.HistoryAsync), "Employee,Approver,Finance,Auditor")]
    [DataRow(typeof(AdminUsersController), nameof(AdminUsersController.ListAsync), "Admin")]
    [DataRow(typeof(AdminUsersController), nameof(AdminUsersController.UpdateRolesAsync), "Admin")]
    public void Endpoint_AsksForExactlyTheRolesOfTheMatrix(Type controller, string action, string expectedRoles)
    {
        MethodInfo method = controller.GetMethod(action)!;
        AuthorizeAttribute[] attributes = method.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .ToArray();

        Assert.HasCount(1, attributes);
        Assert.AreEqual(Normalize(expectedRoles), Normalize(attributes[0].Roles));
    }

    /// <summary>No action of these controllers, and not the controller itself, is open to anonymous requests.</summary>
    /// <param name="controller">The controller.</param>
    [TestMethod]
    [DataRow(typeof(ExpensesController))]
    [DataRow(typeof(AdminUsersController))]
    public void Controller_AllowsNoAnonymousAccess(Type controller)
    {
        Assert.IsFalse(controller.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any());

        foreach (MethodInfo action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.IsFalse(action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any(), action.Name);
        }
    }

    private static string Normalize(string? roles)
    {
        string[] names = (roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join(',', names.Order(StringComparer.Ordinal));
    }
}
