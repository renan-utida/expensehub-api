using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the registration and role administration rules, using a fake store and no database.
/// </summary>
[TestClass]
public sealed class UserAccountServiceTests
{
    private static readonly string _credential = new string('a', 12);
    private static readonly string[] _unknownRoleOnly = { "Boss" };
    private static readonly string[] _repeatedEmployee = { "Employee", "employee", "EMPLOYEE" };
    private static readonly string[] _sameRolesInOtherCase = { "approver", "employee" };

    /// <summary>A new e-mail is registered and the user starts without any role.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task RegisterAsync_NewEmail_CreatesTheUserWithoutAnyRole()
    {
        var store = new FakeUserAccountStore();

        RegistrationResult result = await new UserAccountService(store).RegisterAsync("new@expensehub.local", _credential);

        Assert.AreEqual(RegistrationStatus.Created, result.Status);
        Assert.IsNotNull(result.User);
        Assert.IsEmpty(result.User.Roles);
        Assert.IsEmpty(store.RolesOf(result.User.Id));
    }

    /// <summary>Registering never touches roles, so registration cannot promote anyone.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task RegisterAsync_NewEmail_NeverReplacesRoles()
    {
        var store = new FakeUserAccountStore();

        await new UserAccountService(store).RegisterAsync("new@expensehub.local", _credential);

        Assert.AreEqual(0, store.ReplaceRolesCalls);
    }

    /// <summary>An e-mail already registered, even in another letter case, is a duplicate and creates no user.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task RegisterAsync_EmailAlreadyRegisteredInOtherCase_ReturnsDuplicateEmail()
    {
        var store = new FakeUserAccountStore();
        store.GivenUser("person@expensehub.local");

        RegistrationResult result = await new UserAccountService(store).RegisterAsync("PERSON@expensehub.local", _credential);

        Assert.AreEqual(RegistrationStatus.DuplicateEmail, result.Status);
        Assert.IsNull(result.User);
        Assert.AreEqual(1, store.UserCount());
    }

    /// <summary>The listing shows every user with its own roles.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task ListAsync_ReturnsEveryUserWithItsRoles()
    {
        var store = new FakeUserAccountStore();
        store.GivenUser("a@expensehub.local", AppRoles.Employee, AppRoles.Approver);
        store.GivenUser("b@expensehub.local");

        var users = await new UserAccountService(store).ListAsync();

        Assert.HasCount(2, users);
        CollectionAssert.AreEqual(new[] { AppRoles.Approver, AppRoles.Employee }, users[0].Roles.ToArray());
        Assert.IsEmpty(users[1].Roles);
    }

    /// <summary>Known roles are granted to the user.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_KnownRoles_GrantsThem()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local");

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync("admin-id", userId, new[] { AppRoles.Employee, AppRoles.Approver });

        Assert.AreEqual(UpdateRolesStatus.Updated, result.Status);
        CollectionAssert.AreEquivalent(new[] { AppRoles.Employee, AppRoles.Approver }, store.RolesOf(userId).ToArray());
    }

    /// <summary>A role written in another letter case or with spaces is stored with its canonical name.</summary>
    /// <param name="requested">The role as the client wrote it.</param>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    [DataRow("employee")]
    [DataRow("EMPLOYEE")]
    [DataRow("  Employee  ")]
    public async Task UpdateRolesAsync_RoleInOtherCase_StoresTheCanonicalName(string requested)
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local");

        await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, new[] { requested });

        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, store.RolesOf(userId).ToArray());
    }

    /// <summary>A role left out of the list is removed from the user.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_RoleLeftOut_RemovesIt()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee, AppRoles.Finance);

        await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, new[] { AppRoles.Employee });

        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, store.RolesOf(userId).ToArray());
    }

    /// <summary>An empty list removes every role of the user.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_EmptyList_RemovesEveryRole()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee, AppRoles.Auditor);

        UpdateRolesResult result = await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, Array.Empty<string>());

        Assert.AreEqual(UpdateRolesStatus.Updated, result.Status);
        Assert.IsEmpty(store.RolesOf(userId));
    }

    /// <summary>A role repeated in the request is kept once.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_DuplicatedNames_KeepsEachRoleOnce()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local");

        await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, _repeatedEmployee);

        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, store.RolesOf(userId).ToArray());
    }

    /// <summary>An unknown role is rejected, nothing is written and no role is created implicitly.</summary>
    /// <param name="unknownRole">A name that is not a known role.</param>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    [DataRow("Superuser")]
    [DataRow("Admin2")]
    [DataRow("")]
    [DataRow("   ")]
    public async Task UpdateRolesAsync_UnknownRole_IsRejectedAndWritesNothing(string unknownRole)
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee);

        UpdateRolesResult result = await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, new[] { unknownRole });

        Assert.AreEqual(UpdateRolesStatus.InvalidRoles, result.Status);
        Assert.HasCount(1, result.InvalidRoles);
        Assert.AreEqual(0, store.ReplaceRolesCalls);
        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, store.RolesOf(userId).ToArray());
    }

    /// <summary>One unknown role among valid ones rejects the whole request, so nothing is partially applied.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_OneUnknownAmongKnownRoles_RejectsTheWholeRequest()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local");

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync("admin-id", userId, new[] { AppRoles.Employee, "Boss" });

        Assert.AreEqual(UpdateRolesStatus.InvalidRoles, result.Status);
        CollectionAssert.AreEqual(_unknownRoleOnly, result.InvalidRoles.ToArray());
        Assert.IsEmpty(store.RolesOf(userId));
    }

    /// <summary>A missing role list is rejected and never read as "remove every role".</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_MissingRoleList_IsRejectedAndKeepsTheRoles()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee);

        UpdateRolesResult result = await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, null);

        Assert.AreEqual(UpdateRolesStatus.InvalidRoles, result.Status);
        CollectionAssert.AreEqual(new[] { AppRoles.Employee }, store.RolesOf(userId).ToArray());
    }

    /// <summary>A user that does not exist is reported as not found.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_UnknownUser_ReturnsUserNotFound()
    {
        var store = new FakeUserAccountStore();

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync("admin-id", "missing-id", new[] { AppRoles.Employee });

        Assert.AreEqual(UpdateRolesStatus.UserNotFound, result.Status);
        Assert.AreEqual(0, store.ReplaceRolesCalls);
    }

    /// <summary>An invalid role is reported as such before the user is looked up.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_UnknownRoleForMissingUser_ReportsTheInvalidRoleFirst()
    {
        var store = new FakeUserAccountStore();

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync("admin-id", "missing-id", _unknownRoleOnly);

        Assert.AreEqual(UpdateRolesStatus.InvalidRoles, result.Status);
    }

    /// <summary>An Admin cannot remove their own Admin role by listing other roles.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_AdminRemovesOwnAdminRole_IsRefusedAndKeepsTheRoles()
    {
        var store = new FakeUserAccountStore();
        string adminId = store.GivenUser("admin@expensehub.local", AppRoles.Admin);

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync(adminId, adminId, new[] { AppRoles.Employee });

        Assert.AreEqual(UpdateRolesStatus.CannotRemoveOwnAdminRole, result.Status);
        Assert.AreEqual(0, store.ReplaceRolesCalls);
        CollectionAssert.AreEqual(new[] { AppRoles.Admin }, store.RolesOf(adminId).ToArray());
    }

    /// <summary>An Admin cannot remove their own Admin role by sending an empty list either.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_AdminSendsEmptyListForSelf_IsRefusedAndKeepsTheRoles()
    {
        var store = new FakeUserAccountStore();
        string adminId = store.GivenUser("admin@expensehub.local", AppRoles.Admin);

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync(adminId, adminId, Array.Empty<string>());

        Assert.AreEqual(UpdateRolesStatus.CannotRemoveOwnAdminRole, result.Status);
        CollectionAssert.AreEqual(new[] { AppRoles.Admin }, store.RolesOf(adminId).ToArray());
    }

    /// <summary>An Admin can keep their Admin role and accumulate other roles.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_AdminKeepsOwnAdminRoleAndAddsAnother_IsAllowed()
    {
        var store = new FakeUserAccountStore();
        string adminId = store.GivenUser("admin@expensehub.local", AppRoles.Admin);

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync(adminId, adminId, new[] { AppRoles.Admin, AppRoles.Employee });

        Assert.AreEqual(UpdateRolesStatus.Updated, result.Status);
        CollectionAssert.AreEquivalent(new[] { AppRoles.Admin, AppRoles.Employee }, store.RolesOf(adminId).ToArray());
    }

    /// <summary>An Admin can remove the Admin role of another user, because the acting Admin keeps theirs.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_AdminRemovesAnotherAdminRole_IsAllowed()
    {
        var store = new FakeUserAccountStore();
        string actorId = store.GivenUser("admin@expensehub.local", AppRoles.Admin);
        string otherId = store.GivenUser("other@expensehub.local", AppRoles.Admin);

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync(actorId, otherId, Array.Empty<string>());

        Assert.AreEqual(UpdateRolesStatus.Updated, result.Status);
        Assert.IsEmpty(store.RolesOf(otherId));
        CollectionAssert.AreEqual(new[] { AppRoles.Admin }, store.RolesOf(actorId).ToArray());
    }

    /// <summary>Sending the roles the user already has changes nothing, so the security stamp is not renewed.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_SameRoles_DoesNotWrite()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee, AppRoles.Approver);

        UpdateRolesResult result = await new UserAccountService(store)
            .UpdateRolesAsync("admin-id", userId, _sameRolesInOtherCase);

        Assert.AreEqual(UpdateRolesStatus.Updated, result.Status);
        Assert.AreEqual(0, store.ReplaceRolesCalls);
    }

    /// <summary>A change of roles is written once.</summary>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    public async Task UpdateRolesAsync_ChangedRoles_WritesOnce()
    {
        var store = new FakeUserAccountStore();
        string userId = store.GivenUser("a@expensehub.local", AppRoles.Employee);

        await new UserAccountService(store).UpdateRolesAsync("admin-id", userId, new[] { AppRoles.Finance });

        Assert.AreEqual(1, store.ReplaceRolesCalls);
    }
}
