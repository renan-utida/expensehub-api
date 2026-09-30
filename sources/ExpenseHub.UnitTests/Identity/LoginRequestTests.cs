using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using ExpenseHub.Api.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Identity;

/// <summary>
/// Tests of the data-annotation validation of the login request.
/// </summary>
[TestClass]
public sealed class LoginRequestTests
{
    /// <summary>Valid credentials pass validation.</summary>
    [TestMethod]
    public void Validate_ValidCredentials_HasNoErrors()
    {
        var request = new LoginRequest { Email = "user@expensehub.local", Password = "Some#Password1" };

        Assert.IsEmpty(Validate(request));
    }

    /// <summary>A missing e-mail is rejected.</summary>
    /// <param name="email">An e-mail that is missing or empty.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Validate_MissingEmail_ReportsEmailError(string? email)
    {
        var request = new LoginRequest { Email = email!, Password = "Some#Password1" };

        CollectionAssert.Contains(Validate(request), nameof(LoginRequest.Email));
    }

    /// <summary>An e-mail without a valid format is rejected.</summary>
    /// <param name="email">A malformed e-mail.</param>
    [TestMethod]
    [DataRow("not-an-email")]
    [DataRow("user@")]
    [DataRow("@expensehub.local")]
    public void Validate_MalformedEmail_ReportsEmailError(string email)
    {
        var request = new LoginRequest { Email = email, Password = "Some#Password1" };

        CollectionAssert.Contains(Validate(request), nameof(LoginRequest.Email));
    }

    /// <summary>An e-mail longer than the allowed maximum is rejected.</summary>
    [TestMethod]
    public void Validate_EmailLongerThanMaximum_ReportsEmailError()
    {
        string email = new string('a', LoginRequest.EmailMaxLength) + "@expensehub.local";
        var request = new LoginRequest { Email = email, Password = "Some#Password1" };

        CollectionAssert.Contains(Validate(request), nameof(LoginRequest.Email));
    }

    /// <summary>A missing password is rejected.</summary>
    /// <param name="password">A password that is missing or empty.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Validate_MissingPassword_ReportsPasswordError(string? password)
    {
        var request = new LoginRequest { Email = "user@expensehub.local", Password = password! };

        CollectionAssert.Contains(Validate(request), nameof(LoginRequest.Password));
    }

    /// <summary>A password longer than the allowed maximum is rejected, so huge input is never hashed.</summary>
    [TestMethod]
    public void Validate_PasswordLongerThanMaximum_ReportsPasswordError()
    {
        var request = new LoginRequest
        {
            Email = "user@expensehub.local",
            Password = new string('x', LoginRequest.PasswordMaxLength + 1),
        };

        CollectionAssert.Contains(Validate(request), nameof(LoginRequest.Password));
    }

    /// <summary>A password with exactly the maximum length is accepted.</summary>
    [TestMethod]
    public void Validate_PasswordWithMaximumLength_HasNoErrors()
    {
        var request = new LoginRequest
        {
            Email = "user@expensehub.local",
            Password = new string('x', LoginRequest.PasswordMaxLength),
        };

        Assert.IsEmpty(Validate(request));
    }

    private static List<string> Validate(LoginRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);

        return results.SelectMany(result => result.MemberNames).ToList();
    }
}
