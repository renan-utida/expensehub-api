using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the access matrix of requesting a report. The expected table is written by hand, row by row, from the table of the
/// assignment (Employee asks for Mine, Auditor asks for Global, both roles ask for both, and Admin, Approver and Finance alone
/// ask for nothing). It is never computed from the rule under test, so the rule cannot agree with itself by accident.
/// </summary>
[TestClass]
public sealed class ReportAccessTests
{
    /// <summary>
    /// Every combination of the five roles against the two scopes: 32 rows, 64 decisions. The roles are separated by comma;
    /// an empty text is a user with no role.
    /// </summary>
    /// <param name="roles">The roles of the user.</param>
    /// <param name="mine">Whether the scope Mine is allowed.</param>
    /// <param name="global">Whether the scope Global is allowed.</param>
    [TestMethod]
    [DataRow("", false, false)]
    [DataRow("Admin", false, false)]
    [DataRow("Employee", true, false)]
    [DataRow("Approver", false, false)]
    [DataRow("Finance", false, false)]
    [DataRow("Auditor", false, true)]
    [DataRow("Admin,Employee", true, false)]
    [DataRow("Admin,Approver", false, false)]
    [DataRow("Admin,Finance", false, false)]
    [DataRow("Admin,Auditor", false, true)]
    [DataRow("Employee,Approver", true, false)]
    [DataRow("Employee,Finance", true, false)]
    [DataRow("Employee,Auditor", true, true)]
    [DataRow("Approver,Finance", false, false)]
    [DataRow("Approver,Auditor", false, true)]
    [DataRow("Finance,Auditor", false, true)]
    [DataRow("Admin,Employee,Approver", true, false)]
    [DataRow("Admin,Employee,Finance", true, false)]
    [DataRow("Admin,Employee,Auditor", true, true)]
    [DataRow("Admin,Approver,Finance", false, false)]
    [DataRow("Admin,Approver,Auditor", false, true)]
    [DataRow("Admin,Finance,Auditor", false, true)]
    [DataRow("Employee,Approver,Finance", true, false)]
    [DataRow("Employee,Approver,Auditor", true, true)]
    [DataRow("Employee,Finance,Auditor", true, true)]
    [DataRow("Approver,Finance,Auditor", false, true)]
    [DataRow("Admin,Employee,Approver,Finance", true, false)]
    [DataRow("Admin,Employee,Approver,Auditor", true, true)]
    [DataRow("Admin,Employee,Finance,Auditor", true, true)]
    [DataRow("Admin,Approver,Finance,Auditor", false, true)]
    [DataRow("Employee,Approver,Finance,Auditor", true, true)]
    [DataRow("Admin,Employee,Approver,Finance,Auditor", true, true)]
    public void CanRequest_FollowsTheMatrixOfTheAssignment(string roles, bool mine, bool global)
    {
        ExpenseCaller caller = CallerWith(roles);

        Assert.AreEqual(mine, ReportAccess.CanRequest(caller, ReportScope.Mine), $"Mine for [{roles}]");
        Assert.AreEqual(global, ReportAccess.CanRequest(caller, ReportScope.Global), $"Global for [{roles}]");
    }

    /// <summary>The hand-written table above lists every combination of the five roles exactly once, so no row was forgotten.</summary>
    [TestMethod]
    public void Matrix_ListsEveryCombinationOfTheFiveRolesOnce()
    {
        MethodInfo matrix = typeof(ReportAccessTests).GetMethod(nameof(CanRequest_FollowsTheMatrixOfTheAssignment))!;
        string[] listed = matrix.GetCustomAttributes<DataRowAttribute>()
            .Select(row => Canonical(((string)row.Data[0]!).Split(',', StringSplitOptions.RemoveEmptyEntries)))
            .ToArray();

        string[] everyCombination = EveryCombinationOf(AppRoles.All).Select(Canonical).ToArray();

        Assert.HasCount(32, listed);
        Assert.HasCount(32, listed.Distinct(StringComparer.Ordinal).ToArray());
        CollectionAssert.AreEquivalent(everyCombination, listed);
    }

    /// <summary>The check "has any role that may request" is true for Employee and Auditor alone or together, and false for everything else.</summary>
    /// <param name="roles">The roles of the user.</param>
    /// <param name="expected">Whether some role may request a report.</param>
    [TestMethod]
    [DataRow("", false)]
    [DataRow("Admin", false)]
    [DataRow("Approver", false)]
    [DataRow("Finance", false)]
    [DataRow("Admin,Approver,Finance", false)]
    [DataRow("Employee", true)]
    [DataRow("Auditor", true)]
    [DataRow("Employee,Auditor", true)]
    [DataRow("Admin,Employee", true)]
    [DataRow("Finance,Auditor", true)]
    public void HasRequestRole_IsTrueOnlyForEmployeeOrAuditor(string roles, bool expected)
    {
        Assert.AreEqual(expected, ReportAccess.HasRequestRole(CallerWith(roles)), $"[{roles}]");
    }

    /// <summary>The roles written in the attribute of the route are the same roles the rule accepts, so the attribute and the rule cannot drift apart.</summary>
    [TestMethod]
    public void ReportRequesters_AreExactlyTheRolesTheRuleAccepts()
    {
        string[] attributeRoles = AppRoles.ReportRequesters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string role in AppRoles.All)
        {
            bool inAttribute = attributeRoles.Contains(role, StringComparer.Ordinal);

            Assert.AreEqual(inAttribute, ReportAccess.HasRequestRole(CallerWith(role)), role);
        }

        Assert.HasCount(2, attributeRoles);
    }

    /// <summary>Admin, Approver and Finance never change the decision: with or without them, every combination gets the same answer.</summary>
    [TestMethod]
    public void AdminApproverAndFinance_NeverChangeTheDecision()
    {
        string[] neutral = [AppRoles.Admin, AppRoles.Approver, AppRoles.Finance];

        foreach (string[] combination in EveryCombinationOf(AppRoles.All))
        {
            ExpenseCaller with = CallerWith(string.Join(',', combination));
            ExpenseCaller without = CallerWith(string.Join(',', combination.Except(neutral, StringComparer.Ordinal)));

            Assert.AreEqual(ReportAccess.CanRequest(without, ReportScope.Mine), ReportAccess.CanRequest(with, ReportScope.Mine), Canonical(combination));
            Assert.AreEqual(ReportAccess.CanRequest(without, ReportScope.Global), ReportAccess.CanRequest(with, ReportScope.Global), Canonical(combination));
            Assert.AreEqual(ReportAccess.HasRequestRole(without), ReportAccess.HasRequestRole(with), Canonical(combination));
        }
    }

    /// <summary>The role names are compared exactly: other letter case, spaces and names that are not roles give nothing.</summary>
    /// <param name="role">A role name that is not one of the five.</param>
    [TestMethod]
    [DataRow("employee")]
    [DataRow("EMPLOYEE")]
    [DataRow(" Employee")]
    [DataRow("Employee ")]
    [DataRow("auditor")]
    [DataRow("AUDITOR")]
    [DataRow("Manager")]
    [DataRow("Owner")]
    [DataRow("Mine")]
    [DataRow("Global")]
    public void RoleNames_AreComparedExactly(string role)
    {
        ExpenseCaller caller = ReportTestData.Caller("user-1", role);

        Assert.IsFalse(ReportAccess.HasRequestRole(caller));
        Assert.IsFalse(ReportAccess.CanRequest(caller, ReportScope.Mine));
        Assert.IsFalse(ReportAccess.CanRequest(caller, ReportScope.Global));
    }

    /// <summary>A scope that is not one of the two is never allowed, even to a user with both roles.</summary>
    /// <param name="scopeNumber">A number that is not the value of a scope.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(2)]
    [DataRow(99)]
    public void CanRequest_UndefinedScope_IsNeverAllowed(int scopeNumber)
    {
        ExpenseCaller both = CallerWith("Employee,Auditor");

        Assert.IsFalse(ReportAccess.CanRequest(both, (ReportScope)scopeNumber));
    }

    /// <summary>The rules do not accept a missing user.</summary>
    [TestMethod]
    public void MissingCaller_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => ReportAccess.HasRequestRole(null!));
        Assert.Throws<ArgumentNullException>(() => ReportAccess.CanRequest(null!, ReportScope.Mine));
    }

    private static ExpenseCaller CallerWith(string roles)
    {
        return ReportTestData.Caller("user-1", roles.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string Canonical(IEnumerable<string> roles)
    {
        return string.Join(',', roles.Order(StringComparer.Ordinal));
    }

    private static List<string[]> EveryCombinationOf(IReadOnlyList<string> roles)
    {
        var combinations = new List<string[]>();

        for (int mask = 0; mask < 1 << roles.Count; mask++)
        {
            combinations.Add(roles.Where((_, index) => (mask & (1 << index)) != 0).ToArray());
        }

        return combinations;
    }
}
