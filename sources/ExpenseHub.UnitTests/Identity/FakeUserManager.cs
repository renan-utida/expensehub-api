using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Hand-written fake of the Identity user manager for the security stamp middleware tests. It knows the current stamp of a few
/// users, answers only the two lookups the middleware makes, and records the users it was asked for, so a test can tell that
/// the middleware did not look anyone up. Everything else of the manager is unused and its store refuses every call.
/// </summary>
internal sealed class FakeUserManager : UserManager<IdentityUser>
{
    private static readonly UnusedUserStore _unusedStore = new();

    private readonly Dictionary<string, string> _stampsByUserId = new();
    private readonly List<string> _lookups = new();

    /// <summary>Initializes a new instance of the <see cref="FakeUserManager"/> class.</summary>
    public FakeUserManager()
        : base(
            _unusedStore,
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<IdentityUser>(),
            Array.Empty<IUserValidator<IdentityUser>>(),
            Array.Empty<IPasswordValidator<IdentityUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<IdentityUser>>.Instance)
    {
    }

    /// <summary>Gets the identifiers of the users the middleware asked for, in order.</summary>
    public IReadOnlyList<string> Lookups => _lookups;

    /// <summary>Registers a user that exists, with its current security stamp.</summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="currentStamp">The stamp stored for the user.</param>
    public void Given(string userId, string currentStamp)
    {
        _stampsByUserId[userId] = currentStamp;
    }

    /// <inheritdoc />
    public override Task<IdentityUser?> FindByIdAsync(string userId)
    {
        _lookups.Add(userId);

        return Task.FromResult<IdentityUser?>(_stampsByUserId.ContainsKey(userId) ? new IdentityUser { Id = userId } : null);
    }

    /// <inheritdoc />
    public override Task<string> GetSecurityStampAsync(IdentityUser user)
    {
        return Task.FromResult(_stampsByUserId[user.Id]);
    }

    private sealed class UnusedUserStore : IUserStore<IdentityUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
