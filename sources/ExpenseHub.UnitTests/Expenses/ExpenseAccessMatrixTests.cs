using System;
using System.Collections.Generic;
using System.Linq;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Expenses;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Expenses;

/// <summary>
/// Tests of the authorization matrix: the role, the ownership and the state of an expense combined in one rule.
/// The first group checks named cases of the matrix; the second checks security properties over every combination
/// of roles, actions, states and owners.
/// </summary>
[TestClass]
public sealed class ExpenseAccessMatrixTests
{
    private const string Me = "me";
    private const string Someone = "someone";

    /// <summary>The matrix of the editing and submission of drafts: role Employee, the owner, and the Draft state.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    /// <param name="action">The action.</param>
    /// <param name="owner">Whose expense it is: <c>own</c> or <c>other</c>.</param>
    /// <param name="status">The state of the expense.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow("Employee", ExpenseAction.Edit, "own", ExpenseStatus.Draft, AccessDecision.Allowed)]
    [DataRow("Employee", ExpenseAction.Submit, "own", ExpenseStatus.Draft, AccessDecision.Allowed)]
    [DataRow("Employee", ExpenseAction.Edit, "own", ExpenseStatus.Submitted, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Submit, "own", ExpenseStatus.Submitted, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Submit, "own", ExpenseStatus.Approved, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Submit, "own", ExpenseStatus.Rejected, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Submit, "own", ExpenseStatus.Paid, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Edit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Edit, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Edit, "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Edit, "other", ExpenseStatus.Rejected, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Edit, "other", ExpenseStatus.Paid, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Submit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Submit, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Submit, "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Submit, "other", ExpenseStatus.Rejected, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Submit, "other", ExpenseStatus.Paid, AccessDecision.Forbidden)]
    [DataRow("Employee", ExpenseAction.Edit, "own", ExpenseStatus.Approved, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Edit, "own", ExpenseStatus.Rejected, AccessDecision.WrongState)]
    [DataRow("Employee", ExpenseAction.Edit, "own", ExpenseStatus.Paid, AccessDecision.WrongState)]
    [DataRow("Employee,Auditor", ExpenseAction.Edit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Auditor", ExpenseAction.Submit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Auditor", ExpenseAction.Submit, "other", ExpenseStatus.Paid, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Edit, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Submit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Finance", ExpenseAction.Edit, "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Employee,Finance", ExpenseAction.Submit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver,Finance", ExpenseAction.Submit, "own", ExpenseStatus.Draft, AccessDecision.Allowed)]
    [DataRow("Admin,Employee", ExpenseAction.Edit, "own", ExpenseStatus.Draft, AccessDecision.Allowed)]
    [DataRow("Approver", ExpenseAction.Edit, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Finance", ExpenseAction.Submit, "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Auditor", ExpenseAction.Edit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Auditor", ExpenseAction.Submit, "other", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Admin", ExpenseAction.Edit, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Admin", ExpenseAction.Submit, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("", ExpenseAction.Edit, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    public void Evaluate_EditAndSubmit_FollowTheMatrix(string roles, ExpenseAction action, string owner, ExpenseStatus status, AccessDecision expected)
    {
        Assert.AreEqual(expected, Evaluate(roles, action, owner, status));
    }

    /// <summary>The matrix of approving and rejecting: role Approver, never the owner, and the Submitted state.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    /// <param name="action">The action.</param>
    /// <param name="owner">Whose expense it is: <c>own</c> or <c>other</c>.</param>
    /// <param name="status">The state of the expense.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow("Approver", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Allowed)]
    [DataRow("Approver", ExpenseAction.Reject, "other", ExpenseStatus.Submitted, AccessDecision.Allowed)]
    [DataRow("Approver", ExpenseAction.Approve, "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Approve, "other", ExpenseStatus.Approved, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Approve, "other", ExpenseStatus.Rejected, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Approve, "other", ExpenseStatus.Paid, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Reject, "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Reject, "other", ExpenseStatus.Approved, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Reject, "other", ExpenseStatus.Rejected, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Reject, "other", ExpenseStatus.Paid, AccessDecision.WrongState)]
    [DataRow("Approver", ExpenseAction.Approve, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Approve, "own", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Reject, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Reject, "own", ExpenseStatus.Paid, AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Approve, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Reject, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Approve, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Reject, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Approve, "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Approver", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Allowed)]
    [DataRow("Employee,Approver", ExpenseAction.Approve, "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Employee,Approver,Finance", ExpenseAction.Approve, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Approver,Finance", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Allowed)]
    [DataRow("Approver,Auditor", ExpenseAction.Approve, "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Admin,Approver", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Allowed)]
    [DataRow("Employee", ExpenseAction.Approve, "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Finance", ExpenseAction.Approve, "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Auditor", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Auditor", ExpenseAction.Reject, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Admin", ExpenseAction.Approve, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("", ExpenseAction.Reject, "other", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    public void Evaluate_ApproveAndReject_FollowTheMatrix(string roles, ExpenseAction action, string owner, ExpenseStatus status, AccessDecision expected)
    {
        Assert.AreEqual(expected, Evaluate(roles, action, owner, status));
    }

    /// <summary>The matrix of paying: role Finance, never the owner, and the Approved state.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    /// <param name="owner">Whose expense it is: <c>own</c> or <c>other</c>.</param>
    /// <param name="status">The state of the expense.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow("Finance", "other", ExpenseStatus.Approved, AccessDecision.Allowed)]
    [DataRow("Finance", "other", ExpenseStatus.Paid, AccessDecision.WrongState)]
    [DataRow("Finance", "other", ExpenseStatus.Submitted, AccessDecision.WrongState)]
    [DataRow("Finance", "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Finance", "other", ExpenseStatus.Rejected, AccessDecision.WrongState)]
    [DataRow("Finance", "own", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Finance", "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Finance", "own", ExpenseStatus.Submitted, AccessDecision.Forbidden)]
    [DataRow("Finance", "own", ExpenseStatus.Paid, AccessDecision.Forbidden)]
    [DataRow("Employee,Finance", "own", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Employee,Finance", "own", ExpenseStatus.Draft, AccessDecision.Forbidden)]
    [DataRow("Employee,Finance", "other", ExpenseStatus.Approved, AccessDecision.Allowed)]
    [DataRow("Employee,Finance", "other", ExpenseStatus.Draft, AccessDecision.WrongState)]
    [DataRow("Employee,Finance", "other", ExpenseStatus.Submitted, AccessDecision.WrongState)]
    [DataRow("Employee,Approver,Finance", "own", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Approver,Finance", "other", ExpenseStatus.Approved, AccessDecision.Allowed)]
    [DataRow("Approver,Finance", "other", ExpenseStatus.Submitted, AccessDecision.WrongState)]
    [DataRow("Admin,Finance", "other", ExpenseStatus.Approved, AccessDecision.Allowed)]
    [DataRow("Employee", "own", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Approver", "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Auditor", "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("Admin", "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    [DataRow("", "other", ExpenseStatus.Approved, AccessDecision.Forbidden)]
    public void Evaluate_Pay_FollowsTheMatrix(string roles, string owner, ExpenseStatus status, AccessDecision expected)
    {
        Assert.AreEqual(expected, Evaluate(roles, ExpenseAction.Pay, owner, status));
    }

    /// <summary>Only a user with the Employee role can create, whatever other roles it has.</summary>
    /// <param name="roles">The roles of the user, separated by comma; empty for none.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow("Employee", AccessDecision.Allowed)]
    [DataRow("Employee,Auditor", AccessDecision.Allowed)]
    [DataRow("Employee,Approver,Finance", AccessDecision.Allowed)]
    [DataRow("Admin,Employee", AccessDecision.Allowed)]
    [DataRow("Approver", AccessDecision.Forbidden)]
    [DataRow("Finance", AccessDecision.Forbidden)]
    [DataRow("Auditor", AccessDecision.Forbidden)]
    [DataRow("Admin", AccessDecision.Forbidden)]
    [DataRow("", AccessDecision.Forbidden)]
    public void Evaluate_Create_NeedsTheEmployeeRole(string roles, AccessDecision expected)
    {
        Assert.AreEqual(expected, ExpenseAccess.Evaluate(Caller(roles), ExpenseAction.Create, null));
    }

    /// <summary>An expense that does not exist is "not found" for every action that needs one, once the role is right.</summary>
    /// <param name="roles">The role the action needs.</param>
    /// <param name="action">The action.</param>
    [TestMethod]
    [DataRow("Employee", ExpenseAction.Edit)]
    [DataRow("Employee", ExpenseAction.Submit)]
    [DataRow("Approver", ExpenseAction.Approve)]
    [DataRow("Approver", ExpenseAction.Reject)]
    [DataRow("Finance", ExpenseAction.Pay)]
    public void Evaluate_MissingExpense_IsNotFoundWhenTheRoleIsRight(string roles, ExpenseAction action)
    {
        Assert.AreEqual(AccessDecision.NotFound, ExpenseAccess.Evaluate(Caller(roles), action, null));
    }

    /// <summary>The role is checked before anything else, so a user without it learns nothing about the expense, not even that it is missing.</summary>
    [TestMethod]
    public void Evaluate_WithoutTheRole_IsForbiddenEvenForAMissingExpense()
    {
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccess.Evaluate(Caller("Auditor"), ExpenseAction.Edit, null));
        Assert.AreEqual(AccessDecision.Forbidden, ExpenseAccess.Evaluate(Caller("Admin"), ExpenseAction.Pay, null));
    }

    /// <summary>Nobody decides on their own expense: approving, rejecting and paying it is forbidden for every combination of roles and states.</summary>
    [TestMethod]
    public void Evaluate_NobodyDecidesOnTheirOwnExpense()
    {
        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in Combinations(DecisionActions))
        {
            Assert.AreNotEqual(
                AccessDecision.Allowed,
                Evaluate(roles, action, "own", status),
                $"{roles} {action} own {status}");
        }
    }

    /// <summary>Ownership compares the identifiers exactly: an identifier that differs only in letter case is another user, so that user does not edit the expense and does decide on it.</summary>
    /// <param name="roles">The roles of the user who acts, separated by comma.</param>
    /// <param name="action">The action.</param>
    /// <param name="status">The state of the expense.</param>
    /// <param name="userId">The identifier of the user who acts.</param>
    /// <param name="ownerId">The identifier of the owner of the expense.</param>
    /// <param name="expected">The expected decision.</param>
    [TestMethod]
    [DataRow("Employee", ExpenseAction.Edit, ExpenseStatus.Draft, "Owner-1", "owner-1", AccessDecision.Forbidden)]
    [DataRow("Approver", ExpenseAction.Approve, ExpenseStatus.Submitted, "Approver-1", "approver-1", AccessDecision.Allowed)]
    public void Evaluate_OwnershipComparesIdentifiersExactly_DifferenceInLetterCaseIsAnotherUser(
        string roles,
        ExpenseAction action,
        ExpenseStatus status,
        string userId,
        string ownerId,
        AccessDecision expected)
    {
        ExpenseCaller caller = ExpenseTestData.Caller(userId, roles.Split(','));
        Expense expense = ExpenseTestData.ExpenseOf(ownerId, status);

        Assert.AreEqual(expected, ExpenseAccess.Evaluate(caller, action, expense));
    }

    /// <summary>Editing and submitting is only ever allowed to the owner, with the Employee role, on a draft.</summary>
    [TestMethod]
    public void Evaluate_EditAndSubmit_AreOnlyAllowedToTheOwnerWithTheEmployeeRoleOnADraft()
    {
        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in Combinations(WriteActions))
        {
            foreach (string owner in new[] { "own", "other" })
            {
                bool allowed = Evaluate(roles, action, owner, status) == AccessDecision.Allowed;
                bool shouldBeAllowed = HasRole(roles, "Employee") && owner == "own" && status == ExpenseStatus.Draft;

                Assert.AreEqual(shouldBeAllowed, allowed, $"{roles} {action} {owner} {status}");
            }
        }
    }

    /// <summary>Admin and Auditor, with no other role, are never allowed to do anything to an expense: Admin gives no implicit access and the Auditor never writes.</summary>
    [TestMethod]
    public void Evaluate_AdminAndAuditorWithNoOtherRole_AreNeverAllowed()
    {
        foreach (string roles in new[] { "Admin", "Auditor", "Admin,Auditor" })
        {
            foreach (ExpenseAction action in Enum.GetValues<ExpenseAction>())
            {
                foreach (ExpenseStatus status in Enum.GetValues<ExpenseStatus>())
                {
                    foreach (string owner in new[] { "own", "other" })
                    {
                        Expense? expense = action == ExpenseAction.Create ? null : NewExpense(owner, status);

                        Assert.AreNotEqual(
                            AccessDecision.Allowed,
                            ExpenseAccess.Evaluate(Caller(roles), action, expense),
                            $"{roles} {action} {owner} {status}");
                    }
                }
            }
        }
    }

    /// <summary>Rejected and Paid are final states: no action is allowed on an expense in either of them, whatever the roles of the user and the owner.</summary>
    [TestMethod]
    public void Evaluate_RejectedAndPaidAreFinal_NoActionIsAllowedOnThem()
    {
        var finalStates = Combinations(ExistenceOnlyActions)
            .Where(combination => combination.Status is ExpenseStatus.Rejected or ExpenseStatus.Paid);

        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in finalStates)
        {
            foreach (string owner in new[] { "own", "other" })
            {
                Assert.AreNotEqual(
                    AccessDecision.Allowed,
                    Evaluate(roles, action, owner, status),
                    $"{roles} {action} {owner} {status}");
            }
        }
    }

    /// <summary>An action is only allowed on an expense the user can read, so the write rules never open more than the read rules.</summary>
    [TestMethod]
    public void Evaluate_Allowed_ImpliesTheExpenseIsInsideTheReadScope()
    {
        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in Combinations(ExistenceOnlyActions))
        {
            foreach (string owner in new[] { "own", "other" })
            {
                ExpenseCaller caller = Caller(roles);
                Expense expense = NewExpense(owner, status);

                if (ExpenseAccess.Evaluate(caller, action, expense) == AccessDecision.Allowed)
                {
                    Assert.IsTrue(IsInsideTheReadScope(caller, expense), $"{roles} {action} {owner} {status}");
                }
            }
        }
    }

    /// <summary>"Not found" is only for an expense that does not exist: an existing expense is never "not found" for an action that writes, whatever the roles, the owner and the state.</summary>
    [TestMethod]
    public void Evaluate_ExistingExpense_IsNeverNotFound()
    {
        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in Combinations(ExistenceOnlyActions))
        {
            foreach (string owner in new[] { "own", "other" })
            {
                Assert.AreNotEqual(
                    AccessDecision.NotFound,
                    Evaluate(roles, action, owner, status),
                    $"{roles} {action} {owner} {status}");
            }
        }
    }

    /// <summary>The decision depends only on whether the user has the role of the action, who owns the expense and its state: roles that do not belong to the action never change it, so the read scope cannot creep back into the rule.</summary>
    [TestMethod]
    public void Evaluate_DependsOnlyOnTheRequiredRoleTheOwnerAndTheState()
    {
        var firstDecisions = new Dictionary<(bool HasRole, ExpenseAction Action, string Owner, ExpenseStatus Status), AccessDecision>();

        foreach ((string roles, ExpenseAction action, ExpenseStatus status) in Combinations(ExistenceOnlyActions))
        {
            foreach (string owner in new[] { "own", "other" })
            {
                var key = (HasRole(roles, RequiredRole(action)), action, owner, status);
                AccessDecision decision = Evaluate(roles, action, owner, status);

                if (firstDecisions.TryGetValue(key, out AccessDecision first))
                {
                    Assert.AreEqual(first, decision, $"{roles} {action} {owner} {status}");
                }
                else
                {
                    firstDecisions[key] = decision;
                }
            }
        }
    }

    private static ExpenseAction[] WriteActions { get; } = [ExpenseAction.Edit, ExpenseAction.Submit];

    private static ExpenseAction[] DecisionActions { get; } = [ExpenseAction.Approve, ExpenseAction.Reject, ExpenseAction.Pay];

    private static ExpenseAction[] ExistenceOnlyActions { get; } =
        [ExpenseAction.Edit, ExpenseAction.Submit, ExpenseAction.Approve, ExpenseAction.Reject, ExpenseAction.Pay];

    private static readonly string[] _allRoles = [AppRoles.Employee, AppRoles.Approver, AppRoles.Finance, AppRoles.Auditor, AppRoles.Admin];

    private static AccessDecision Evaluate(string roles, ExpenseAction action, string owner, ExpenseStatus status)
    {
        return ExpenseAccess.Evaluate(Caller(roles), action, NewExpense(owner, status));
    }

    private static ExpenseCaller Caller(string roles)
    {
        return ExpenseTestData.Caller(Me, roles.Length == 0 ? [] : roles.Split(','));
    }

    private static Expense NewExpense(string owner, ExpenseStatus status)
    {
        return ExpenseTestData.ExpenseOf(owner == "own" ? Me : Someone, status);
    }

    private static bool HasRole(string roles, string role)
    {
        return roles.Split(',').Contains(role);
    }

    private static string RequiredRole(ExpenseAction action)
    {
        return action switch
        {
            ExpenseAction.Edit or ExpenseAction.Submit => AppRoles.Employee,
            ExpenseAction.Approve or ExpenseAction.Reject => AppRoles.Approver,
            _ => AppRoles.Finance,
        };
    }

    private static bool IsInsideTheReadScope(ExpenseCaller caller, Expense expense)
    {
        return ExpenseVisibility.ScopeFor(caller).Predicate.Compile()(expense);
    }

    private static IEnumerable<(string Roles, ExpenseAction Action, ExpenseStatus Status)> Combinations(ExpenseAction[] actions)
    {
        for (int mask = 0; mask < (1 << _allRoles.Length); mask++)
        {
            string roles = string.Join(',', _allRoles.Where((_, index) => (mask & (1 << index)) != 0));

            foreach (ExpenseAction action in actions)
            {
                foreach (ExpenseStatus status in Enum.GetValues<ExpenseStatus>())
                {
                    yield return (roles, action, status);
                }
            }
        }
    }
}
