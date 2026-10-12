using System.Security.Claims;
using System.Threading.Tasks;
using ExpenseHub.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the middleware that rejects bearer tokens issued before the security stamp of the user changed (after an Admin
/// changes the roles, the user has to log in again). The comparison itself is tested in <see cref="SecurityStampCheck"/>;
/// these tests check what the middleware does with the result, using a hand-written user manager and no host.
/// </summary>
[TestClass]
public sealed class SecurityStampValidationMiddlewareTests
{
    private const string UserId = "user-1";

    /// <summary>A user whose stamp in the request is the stored one keeps the identity of the request, and the next step runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_StampStillCurrent_KeepsTheUserAndRunsTheNextStep()
    {
        using var userManager = new FakeUserManager();
        userManager.Given(UserId, "stamp-a");
        var next = new CountingNextStep();
        HttpContext context = ContextOf(UserId, "stamp-a");
        ClaimsPrincipal original = context.User;

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.AreSame(original, context.User);
        Assert.IsTrue(context.User.Identity!.IsAuthenticated);
        Assert.AreEqual(1, next.Calls);
    }

    /// <summary>A stamp issued before the roles changed no longer matches: the request becomes anonymous, so the authorization step answers 401, and the next step still runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_StampRenewedAfterTheRolesChanged_BecomesAnonymousAndRunsTheNextStep()
    {
        using var userManager = new FakeUserManager();
        userManager.Given(UserId, "stamp-b");
        var next = new CountingNextStep();
        HttpContext context = ContextOf(UserId, "stamp-a");

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.IsFalse(context.User.Identity!.IsAuthenticated);
        Assert.AreEqual(1, next.Calls);
    }

    /// <summary>A user that no longer exists becomes anonymous, and the next step runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_UserNoLongerExists_BecomesAnonymousAndRunsTheNextStep()
    {
        using var userManager = new FakeUserManager();
        var next = new CountingNextStep();
        HttpContext context = ContextOf(UserId, "stamp-a");

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.IsFalse(context.User.Identity!.IsAuthenticated);
        Assert.AreEqual(1, next.Calls);
        Assert.HasCount(1, userManager.Lookups);
    }

    /// <summary>An authenticated request with no user identifier becomes anonymous without looking anyone up, and the next step runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_PrincipalWithoutUserIdentifier_BecomesAnonymousWithoutLookingAnyoneUp()
    {
        using var userManager = new FakeUserManager();
        userManager.Given(UserId, "stamp-a");
        var next = new CountingNextStep();
        HttpContext context = ContextOf(null, "stamp-a");

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.IsFalse(context.User.Identity!.IsAuthenticated);
        Assert.IsEmpty(userManager.Lookups);
        Assert.AreEqual(1, next.Calls);
    }

    /// <summary>An authenticated request that carries no stamp cannot be proved current: it becomes anonymous, and the next step runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_PrincipalWithoutStamp_BecomesAnonymousAndRunsTheNextStep()
    {
        using var userManager = new FakeUserManager();
        userManager.Given(UserId, "stamp-a");
        var next = new CountingNextStep();
        HttpContext context = ContextOf(UserId, null);

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.IsFalse(context.User.Identity!.IsAuthenticated);
        Assert.AreEqual(1, next.Calls);
    }

    /// <summary>An anonymous request is left alone: the user manager is not asked for anyone, and the next step runs.</summary>
    [TestMethod]
    public async Task InvokeAsync_AnonymousRequest_IsLeftAloneWithoutAskingTheUserManager()
    {
        using var userManager = new FakeUserManager();
        var next = new CountingNextStep();
        var context = new DefaultHttpContext();
        ClaimsPrincipal original = context.User;

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, DefaultOptions());

        Assert.AreSame(original, context.User);
        Assert.IsEmpty(userManager.Lookups);
        Assert.AreEqual(1, next.Calls);
    }

    /// <summary>The stamp is read from the claim that the Identity options name, and not from a fixed claim type.</summary>
    [TestMethod]
    public async Task InvokeAsync_StampClaimNamedByTheIdentityOptions_IsTheOneCompared()
    {
        using var userManager = new FakeUserManager();
        userManager.Given(UserId, "stamp-a");
        var next = new CountingNextStep();
        var options = new IdentityOptions();
        options.ClaimsIdentity.SecurityStampClaimType = "custom.stamp";
        HttpContext context = ContextOf(UserId, "stamp-a", "custom.stamp");
        ClaimsPrincipal original = context.User;

        await new SecurityStampValidationMiddleware(next.Invoke).InvokeAsync(context, userManager, Options.Create(options));

        Assert.AreSame(original, context.User);
        Assert.AreEqual(1, next.Calls);
    }

    private static IOptions<IdentityOptions> DefaultOptions()
    {
        return Options.Create(new IdentityOptions());
    }

    private static DefaultHttpContext ContextOf(string? userId, string? claimedStamp, string? stampClaimType = null)
    {
        var identity = new ClaimsIdentity("test");

        if (userId is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (claimedStamp is not null)
        {
            identity.AddClaim(new Claim(stampClaimType ?? new IdentityOptions().ClaimsIdentity.SecurityStampClaimType, claimedStamp));
        }

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private sealed class CountingNextStep
    {
        public int Calls { get; private set; }

        public Task Invoke(HttpContext context)
        {
            Calls++;

            return Task.CompletedTask;
        }
    }
}
