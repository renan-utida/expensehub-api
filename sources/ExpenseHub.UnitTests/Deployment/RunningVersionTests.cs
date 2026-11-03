using ExpenseHub.Api.Deployment;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Deployment;

/// <summary>
/// Tests of the commit that is written in the log when a process starts. The commit comes from an environment variable, so only
/// a commit id is accepted and anything else is shown as unknown instead of being logged as free text.
/// </summary>
[TestClass]
public sealed class RunningVersionTests
{
    private const string FullSha = "78bca7d00a785bb37bcedc5b40bcd14adffa2535";

    /// <summary>A full commit SHA is kept as it is.</summary>
    [TestMethod]
    public void Normalize_KeepsAFullCommitSha()
    {
        Assert.AreEqual(FullSha, RunningVersion.Normalize(FullSha));
    }

    /// <summary>A short commit id of seven characters is accepted.</summary>
    [TestMethod]
    public void Normalize_AcceptsAShortCommitId()
    {
        Assert.AreEqual("78bca7d", RunningVersion.Normalize("78bca7d"));
    }

    /// <summary>The longest accepted id is a 64-character one.</summary>
    [TestMethod]
    public void Normalize_AcceptsTheLongestId()
    {
        string longest = new('a', 64);

        Assert.AreEqual(longest, RunningVersion.Normalize(longest));
    }

    /// <summary>Upper case letters are written in lower case, so the same commit always looks the same.</summary>
    [TestMethod]
    public void Normalize_WritesTheLettersInLowerCase()
    {
        Assert.AreEqual(FullSha, RunningVersion.Normalize(FullSha.ToUpperInvariant()));
    }

    /// <summary>A missing, empty or blank value is shown as unknown.</summary>
    /// <param name="commit">The value read from the configuration.</param>
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Normalize_ReturnsUnknown_WhenTheCommitIsMissing(string? commit)
    {
        Assert.AreEqual(RunningVersion.Unknown, RunningVersion.Normalize(commit));
    }

    /// <summary>A value that is not a commit id is never logged: it becomes unknown.</summary>
    /// <param name="commit">The value read from the configuration.</param>
    [TestMethod]
    [DataRow("unknown")]
    [DataRow("main")]
    [DataRow("v1.0.0-release")]
    [DataRow("78bca7g")]
    [DataRow("78bca7d 78bca7d")]
    [DataRow("78bca7d\n")]
    [DataRow("78bca7d\r\nInjected log line")]
    [DataRow("78bca7d;rm -rf")]
    public void Normalize_ReturnsUnknown_WhenTheValueIsNotACommitId(string commit)
    {
        Assert.AreEqual(RunningVersion.Unknown, RunningVersion.Normalize(commit));
    }

    /// <summary>An id that is too short or too long to be a commit id becomes unknown.</summary>
    [TestMethod]
    public void Normalize_ReturnsUnknown_WhenTheLengthIsOutOfRange()
    {
        Assert.AreEqual(RunningVersion.Unknown, RunningVersion.Normalize("78bca7"));
        Assert.AreEqual(RunningVersion.Unknown, RunningVersion.Normalize(new string('a', 65)));
    }
}
