using System.Linq;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of the settings of the connection to RabbitMQ: the defaults, the validation that names the missing or invalid
/// settings, and that the list never repeats a value, so it can be logged without leaking the credentials. The credentials of the
/// tests are built at run time, never written in the source.
/// </summary>
[TestClass]
public sealed class RabbitMqOptionsTests
{
    /// <summary>The defaults are a local broker with the default port, the root virtual host and the documented queue, and no credentials.</summary>
    [TestMethod]
    public void Defaults_AreALocalBrokerWithoutCredentials()
    {
        var options = new RabbitMqOptions();

        Assert.AreEqual("localhost", options.Host);
        Assert.AreEqual(5672, options.Port);
        Assert.AreEqual("/", options.VirtualHost);
        Assert.AreEqual("expensehub.reports", options.QueueName);
        Assert.AreEqual(5, options.PublishTimeoutSeconds);
        Assert.AreEqual(5, options.TransientRetryDelaySeconds);
        Assert.IsNull(options.UserName);
        Assert.IsNull(options.Password);
    }

    /// <summary>Without credentials the settings are not usable, and the user name and the password are the only ones named.</summary>
    [TestMethod]
    public void Validate_Defaults_NameOnlyTheCredentials()
    {
        string[] invalid = new RabbitMqOptions().Validate().ToArray();

        string[] expected = ["UserName", "Password"];
        CollectionAssert.AreEquivalent(expected, invalid);
    }

    /// <summary>With the credentials given, the defaults are enough.</summary>
    [TestMethod]
    public void Validate_WithCredentials_IsValid()
    {
        RabbitMqOptions options = ValidOptions();

        Assert.IsEmpty(options.Validate());
    }

    /// <summary>A blank user name or password is a missing setting.</summary>
    /// <param name="blank">A value that is not a credential.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow(null)]
    public void Validate_BlankCredentials_AreNamed(string? blank)
    {
        RabbitMqOptions options = ValidOptions();
        options.UserName = blank;
        options.Password = blank;

        string[] expected = ["UserName", "Password"];
        CollectionAssert.AreEquivalent(expected, options.Validate().ToArray());
    }

    /// <summary>A blank host, virtual host or queue name is named.</summary>
    /// <param name="blank">A value that is not a name.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Validate_BlankNames_AreNamed(string blank)
    {
        RabbitMqOptions options = ValidOptions();
        options.Host = blank;
        options.VirtualHost = blank;
        options.QueueName = blank;

        string[] expected = ["Host", "VirtualHost", "QueueName"];
        CollectionAssert.AreEquivalent(expected, options.Validate().ToArray());
    }

    /// <summary>A port outside 1 to 65535 is named.</summary>
    /// <param name="port">The port.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(65536)]
    public void Validate_PortOutOfRange_IsNamed(int port)
    {
        RabbitMqOptions options = ValidOptions();
        options.Port = port;

        string[] expected = ["Port"];
        CollectionAssert.AreEqual(expected, options.Validate().ToArray());
    }

    /// <summary>A port at the limits of the range is accepted.</summary>
    /// <param name="port">The port.</param>
    [TestMethod]
    [DataRow(1)]
    [DataRow(65535)]
    public void Validate_PortAtTheLimits_IsValid(int port)
    {
        RabbitMqOptions options = ValidOptions();
        options.Port = port;

        Assert.IsEmpty(options.Validate());
    }

    /// <summary>A time that is zero, negative or longer than ten minutes is named.</summary>
    /// <param name="seconds">The number of seconds.</param>
    [TestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(601)]
    public void Validate_TimesOutOfRange_AreNamed(int seconds)
    {
        RabbitMqOptions options = ValidOptions();
        options.PublishTimeoutSeconds = seconds;
        options.TransientRetryDelaySeconds = seconds;

        string[] expected = ["PublishTimeoutSeconds", "TransientRetryDelaySeconds"];
        CollectionAssert.AreEquivalent(expected, options.Validate().ToArray());
    }

    /// <summary>The list names the settings and never repeats a value, so it is safe to log even when a credential is set.</summary>
    [TestMethod]
    public void Validate_NeverRepeatsAValue()
    {
        RabbitMqOptions options = ValidOptions();
        options.Port = 0;
        options.Host = " ";
        string userName = options.UserName!;
        string credential = options.Password!;

        string text = string.Join(';', options.Validate());

        Assert.DoesNotContain(userName, text);
        Assert.DoesNotContain(credential, text);
        Assert.AreEqual("Host;Port", text);
    }

    private static RabbitMqOptions ValidOptions()
    {
        return new RabbitMqOptions
        {
            UserName = new string('u', 8),
            Password = new string('p', 12),
        };
    }
}
