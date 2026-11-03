using System.Collections.Generic;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// The settings of the connection to RabbitMQ, read from the <c>RabbitMq</c> section of the configuration. The user name and the
/// password have no default and never come from a versioned file: they are read from environment variables
/// (<c>RabbitMq__UserName</c> and <c>RabbitMq__Password</c>) or from the user secrets.
/// </summary>
public sealed class RabbitMqOptions
{
    /// <summary>The name of the section of the configuration.</summary>
    public const string SectionName = "RabbitMq";

    private const int MaxPort = 65535;
    private const int MaxSeconds = 600;

    /// <summary>Gets or sets the host name of the broker.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>Gets or sets the port of the broker.</summary>
    public int Port { get; set; } = 5672;

    /// <summary>Gets or sets the virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Gets or sets the name of the queue of the reports.</summary>
    public string QueueName { get; set; } = ReportQueueTopology.DefaultQueueName;

    /// <summary>Gets or sets the user name. It has no default and is not read from a versioned file.</summary>
    public string? UserName { get; set; }

    /// <summary>Gets or sets the password of the user. It has no default and is not read from a versioned file.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets how long the web application waits for the broker to confirm a message, in seconds.</summary>
    public int PublishTimeoutSeconds { get; set; } = 5;

    /// <summary>Gets or sets how long the worker waits before it takes a message back after a transient failure, in seconds.</summary>
    public int TransientRetryDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Lists the settings that are missing or invalid. Only the names of the settings are returned, never their values, so the
    /// list can be logged and shown without leaking the credentials.
    /// </summary>
    /// <returns>The names of the invalid settings; empty when the settings are usable.</returns>
    public IReadOnlyList<string> Validate()
    {
        var invalid = new List<string>();

        if (string.IsNullOrWhiteSpace(Host))
        {
            invalid.Add(nameof(Host));
        }

        if (Port < 1 || Port > MaxPort)
        {
            invalid.Add(nameof(Port));
        }

        if (string.IsNullOrWhiteSpace(VirtualHost))
        {
            invalid.Add(nameof(VirtualHost));
        }

        if (string.IsNullOrWhiteSpace(QueueName))
        {
            invalid.Add(nameof(QueueName));
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            invalid.Add(nameof(UserName));
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            invalid.Add(nameof(Password));
        }

        if (PublishTimeoutSeconds < 1 || PublishTimeoutSeconds > MaxSeconds)
        {
            invalid.Add(nameof(PublishTimeoutSeconds));
        }

        if (TransientRetryDelaySeconds < 1 || TransientRetryDelaySeconds > MaxSeconds)
        {
            invalid.Add(nameof(TransientRetryDelaySeconds));
        }

        return invalid;
    }
}
