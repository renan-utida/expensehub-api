using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the registration request: its validation and the fact that it carries no role.
/// </summary>
[TestClass]
public sealed class RegisterRequestTests
{
    private static readonly string _credential = new string('a', 12);

    /// <summary>The request has only an e-mail and a password: there is no role member for the client to fill.</summary>
    [TestMethod]
    public void RegisterRequest_HasNoRoleMember()
    {
        string[] members = typeof(RegisterRequest).GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray();

        CollectionAssert.AreEqual(new[] { nameof(RegisterRequest.Email), nameof(RegisterRequest.Password) }, members);
    }

    /// <summary>Valid data passes validation.</summary>
    [TestMethod]
    public void Validate_ValidData_HasNoErrors()
    {
        var request = new RegisterRequest { Email = "new@expensehub.local", Password = _credential };

        Assert.IsEmpty(Validate(request));
    }

    /// <summary>A missing e-mail is rejected.</summary>
    /// <param name="email">An e-mail that is missing or empty.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Validate_MissingEmail_ReportsEmailError(string? email)
    {
        var request = new RegisterRequest { Email = email!, Password = _credential };

        CollectionAssert.Contains(Validate(request), nameof(RegisterRequest.Email));
    }

    /// <summary>An e-mail without a valid format is rejected.</summary>
    /// <param name="email">A malformed e-mail.</param>
    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("user@")]
    [DataRow("@expensehub.local")]
    public void Validate_MalformedEmail_ReportsEmailError(string email)
    {
        var request = new RegisterRequest { Email = email, Password = _credential };

        CollectionAssert.Contains(Validate(request), nameof(RegisterRequest.Email));
    }

    /// <summary>An e-mail longer than the allowed maximum is rejected.</summary>
    [TestMethod]
    public void Validate_EmailLongerThanMaximum_ReportsEmailError()
    {
        string email = new string('a', LoginRequest.EmailMaxLength) + "@expensehub.local";
        var request = new RegisterRequest { Email = email, Password = _credential };

        CollectionAssert.Contains(Validate(request), nameof(RegisterRequest.Email));
    }

    /// <summary>A missing password is rejected.</summary>
    /// <param name="value">A password that is missing or empty.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Validate_MissingPassword_ReportsPasswordError(string? value)
    {
        var request = new RegisterRequest { Email = "new@expensehub.local", Password = value! };

        CollectionAssert.Contains(Validate(request), nameof(RegisterRequest.Password));
    }

    /// <summary>A password longer than the allowed maximum is rejected.</summary>
    [TestMethod]
    public void Validate_PasswordLongerThanMaximum_ReportsPasswordError()
    {
        var request = new RegisterRequest
        {
            Email = "new@expensehub.local",
            Password = new string('x', LoginRequest.PasswordMaxLength + 1),
        };

        CollectionAssert.Contains(Validate(request), nameof(RegisterRequest.Password));
    }

    private static List<string> Validate(RegisterRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToList();
    }
}
