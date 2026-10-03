using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the read rules of the authorization matrix: what each role, and each combination of roles, can read.
/// The predicate of the scope is compiled and applied to a list in memory, which is the same condition the storage applies in the query.
/// </summary>
[TestClass]
public sealed class ExpenseVisibilityTests
{
    private const string Me = "me";
    private const string Someone = "someone";

    /// <summary>Each role reads what the matrix says, and roles accumulate: the scope is the union of the scopes of the roles.</summary>
    /// <param name="roles">The roles of the user, separated by comma.</param>
    /// <param name="expected">The labels of the expenses the user can read, separated by comma.</param>
    [TestMethod]
    [DataRow("Employee", "myDraft,mySubmitted,myApproved")]
    [DataRow("Approver", "mySubmitted,otherSubmitted")]
    [DataRow("Finance", "myApproved,otherApproved,otherPaid")]
    [DataRow("Auditor", "myDraft,mySubmitted,myApproved,otherDraft,otherSubmitted,otherApproved,otherPaid,otherRejected")]
    [DataRow("Employee,Approver", "myDraft,mySubmitted,myApproved,otherSubmitted")]
    [DataRow("Employee,Finance", "myDraft,mySubmitted,myApproved,otherApproved,otherPaid")]
    [DataRow("Employee,Auditor", "myDraft,mySubmitted,myApproved,otherDraft,otherSubmitted,otherApproved,otherPaid,otherRejected")]
    [DataRow("Approver,Finance", "mySubmitted,otherSubmitted,myApproved,otherApproved,otherPaid")]
    [DataRow("Employee,Approver,Finance", "myDraft,mySubmitted,myApproved,otherSubmitted,otherApproved,otherPaid")]
    [DataRow("Admin,Employee", "myDraft,mySubmitted,myApproved")]
    public void ScopeFor_Roles_ReadsWhatTheMatrixSays(string roles, string expected)
    {
        string[] visible = VisibleLabels(ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, roles.Split(','))));

        CollectionAssert.AreEqual(Sorted(expected), visible);
    }

    /// <summary>The Admin role alone, and a user with no role, read nothing: Admin gives no functional access.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    [TestMethod]
    [DataRow("Admin")]
    [DataRow("")]
    public void ScopeFor_AdminAloneOrNoRole_ReadsNothing(string roles)
    {
        string[] roleNames = roles.Length == 0 ? [] : roles.Split(',');

        ExpenseScope scope = ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, roleNames));

        Assert.IsTrue(scope.IsEmpty);
        Assert.IsEmpty(VisibleLabels(scope));
    }

    /// <summary>A scope that reads something is not empty.</summary>
    /// <param name="role">A role that reads expenses.</param>
    [TestMethod]
    [DataRow("Employee")]
    [DataRow("Approver")]
    [DataRow("Finance")]
    [DataRow("Auditor")]
    public void ScopeFor_RoleThatReadsExpenses_IsNotEmpty(string role)
    {
        Assert.IsFalse(ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, role)).IsEmpty);
    }

    /// <summary>An Employee reads only its own expenses, never the ones of someone else, whatever their state.</summary>
    [TestMethod]
    public void ScopeFor_Employee_NeverReadsExpensesOfSomeoneElse()
    {
        string[] visible = VisibleLabels(ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, "Employee")));

        Assert.IsFalse(visible.Any(label => label.StartsWith("other", StringComparison.Ordinal)));
    }

    /// <summary>The scope of an Employee carries the identifier of the caller, not one chosen by the client.</summary>
    [TestMethod]
    public void ScopeFor_Employee_UsesTheIdentifierOfTheCaller()
    {
        ExpenseScope scope = ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, "Employee"));

        Assert.AreEqual(Me, scope.OwnerId);
    }

    /// <summary>Only the Auditor reads the rejected expenses of others.</summary>
    [TestMethod]
    public void ScopeFor_RejectedOfSomeoneElse_IsReadOnlyByTheAuditor()
    {
        foreach (string role in new[] { "Employee", "Approver", "Finance" })
        {
            Assert.IsFalse(VisibleLabels(ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, role))).Contains("otherRejected"), role);
        }

        Assert.IsTrue(VisibleLabels(ExpenseVisibility.ScopeFor(ExpenseTestData.Caller(Me, "Auditor"))).Contains("otherRejected"));
    }

    private static string[] Sorted(string commaSeparated)
    {
        return commaSeparated.Split(',').OrderBy(label => label, StringComparer.Ordinal).ToArray();
    }

    private static string[] VisibleLabels(ExpenseScope scope)
    {
        Func<Expense, bool> isVisible = scope.Predicate.Compile();

        return Sample()
            .Where(isVisible)
            .Select(expense => expense.Description)
            .OrderBy(label => label, StringComparer.Ordinal)
            .ToArray();
    }

    private static List<Expense> Sample()
    {
        return
        [
            ExpenseTestData.Labeled("myDraft", Me, ExpenseStatus.Draft),
            ExpenseTestData.Labeled("mySubmitted", Me, ExpenseStatus.Submitted),
            ExpenseTestData.Labeled("myApproved", Me, ExpenseStatus.Approved),
            ExpenseTestData.Labeled("otherDraft", Someone, ExpenseStatus.Draft),
            ExpenseTestData.Labeled("otherSubmitted", Someone, ExpenseStatus.Submitted),
            ExpenseTestData.Labeled("otherApproved", Someone, ExpenseStatus.Approved),
            ExpenseTestData.Labeled("otherPaid", Someone, ExpenseStatus.Paid),
            ExpenseTestData.Labeled("otherRejected", Someone, ExpenseStatus.Rejected),
        ];
    }
}
