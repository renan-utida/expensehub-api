using System;
using System.Linq;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the rules of the identity seed, using a fake store and no database.
/// </summary>
[TestClass]
public sealed class IdentitySeederTests
{
    private const string AdminEmail = "admin@expensehub.local";
    private const string AdminPassword = "Fake#Password1";

    /// <summary>The five roles the application needs are the ones in the specification.</summary>
    [TestMethod]
    public void AppRoles_All_ContainsTheFiveRequiredRoles()
    {
        string[] expected = { "Admin", "Employee", "Approver", "Finance", "Auditor" };

        CollectionAssert.AreEquivalent(expected, AppRoles.All.ToArray());
    }

    /// <summary>An empty store ends up with the five required roles.</summary>
    [TestMethod]
    public async Task SeedAsync_EmptyStore_CreatesTheFiveRequiredRoles()
    {
        var store = new FakeIdentitySeedStore();

        await new IdentitySeeder(store).SeedAsync(AdminEmail, AdminPassword);

        CollectionAssert.AreEquivalent(AppRoles.All.ToArray(), store.Roles.ToArray());
    }

    /// <summary>An empty store ends up with exactly one user, the configured Admin, and no other user.</summary>
    [TestMethod]
    public async Task SeedAsync_EmptyStore_CreatesOnlyTheConfiguredAdmin()
    {
        var store = new FakeIdentitySeedStore();

        await new IdentitySeeder(store).SeedAsync(AdminEmail, AdminPassword);

        Assert.HasCount(1, store.Users);
        Assert.AreEqual(AdminEmail, store.Users[0].Email);
    }

    /// <summary>The initial Admin receives only the Admin role, never a functional expense role.</summary>
    [TestMethod]
    public async Task SeedAsync_EmptyStore_GivesTheAdminOnlyTheAdminRole()
    {
        var store = new FakeIdentitySeedStore();

        await new IdentitySeeder(store).SeedAsync(AdminEmail, AdminPassword);

        Assert.AreEqual(AppRoles.Admin, store.Users.Single().Role);
    }

    /// <summary>Running the seed again does not duplicate roles or the Admin.</summary>
    [TestMethod]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateRolesOrAdmin()
    {
        var store = new FakeIdentitySeedStore();
        var seeder = new IdentitySeeder(store);

        await seeder.SeedAsync(AdminEmail, AdminPassword);
        await seeder.SeedAsync(AdminEmail, AdminPassword);

        Assert.AreEqual(AppRoles.All.Count, store.CreateRoleCalls);
        Assert.AreEqual(1, store.CreateUserCalls);
        Assert.HasCount(1, store.Users);
    }

    /// <summary>When an Admin already exists, no other Admin is created, even with a different configured e-mail.</summary>
    [TestMethod]
    public async Task SeedAsync_AdminAlreadyExists_DoesNotCreateAnotherAdmin()
    {
        var store = new FakeIdentitySeedStore();
        store.GivenUser("existing.admin@expensehub.local", AppRoles.Admin);

        await new IdentitySeeder(store).SeedAsync(AdminEmail, AdminPassword);

        Assert.AreEqual(0, store.CreateUserCalls);
        Assert.AreEqual("existing.admin@expensehub.local", store.Users.Single().Email);
    }

    /// <summary>Only the roles that are missing are created, and the existing ones are left alone.</summary>
    [TestMethod]
    public async Task SeedAsync_SomeRolesExist_CreatesOnlyTheMissingRoles()
    {
        var store = new FakeIdentitySeedStore();
        store.GivenRole(AppRoles.Admin);
        store.GivenRole(AppRoles.Employee);

        await new IdentitySeeder(store).SeedAsync(AdminEmail, AdminPassword);

        Assert.AreEqual(AppRoles.All.Count - 2, store.CreateRoleCalls);
        CollectionAssert.AreEquivalent(AppRoles.All.ToArray(), store.Roles.ToArray());
    }

    /// <summary>A missing password stops the seed before anything is written.</summary>
    /// <param name="password">A password that counts as not configured.</param>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task SeedAsync_PasswordNotConfigured_ThrowsAndWritesNothing(string? password)
    {
        var store = new FakeIdentitySeedStore();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new IdentitySeeder(store).SeedAsync(AdminEmail, password));

        StringAssert.Contains(exception.Message, "Seed:Admin:Password");
        Assert.AreEqual(0, store.CreateRoleCalls);
        Assert.AreEqual(0, store.CreateUserCalls);
    }

    /// <summary>A missing e-mail stops the seed before anything is written.</summary>
    /// <param name="email">An e-mail that counts as not configured.</param>
    /// <returns>A task that completes when the assertions are done.</returns>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task SeedAsync_EmailNotConfigured_ThrowsAndWritesNothing(string? email)
    {
        var store = new FakeIdentitySeedStore();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new IdentitySeeder(store).SeedAsync(email, AdminPassword));

        StringAssert.Contains(exception.Message, "Seed:Admin:Email");
        Assert.AreEqual(0, store.CreateRoleCalls);
        Assert.AreEqual(0, store.CreateUserCalls);
    }

    /// <summary>The error for a missing password never contains the password value.</summary>
    [TestMethod]
    public async Task SeedAsync_PasswordNotConfigured_DoesNotLeakAnySecretInTheMessage()
    {
        var store = new FakeIdentitySeedStore();

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new IdentitySeeder(store).SeedAsync(AdminEmail, null));

        Assert.DoesNotContain(AdminPassword, exception.Message, StringComparison.Ordinal);
    }
}
