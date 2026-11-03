using RabbitMQ.Client;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// How the report queue is declared and how its messages are written. The web application (which publishes) and the worker
/// (which consumes) both read this class, so they declare the same queue with the same arguments. The queue is durable and
/// every message is persistent, so a message that the broker confirmed survives a restart of the broker. The default exchange
/// routes by the name of the queue.
/// </summary>
public static class ReportQueueTopology
{
    /// <summary>The default name of the queue. It can be changed by the <c>RabbitMq:QueueName</c> setting.</summary>
    public const string DefaultQueueName = "expensehub.reports";

    /// <summary>The queue survives a restart of the broker.</summary>
    public const bool Durable = true;

    /// <summary>The queue is not tied to one connection, so the worker can reconnect to it.</summary>
    public const bool Exclusive = false;

    /// <summary>The queue is kept when the last consumer leaves, so messages wait while the worker is stopped.</summary>
    public const bool AutoDelete = false;

    /// <summary>How many messages the worker holds without an acknowledgement at the same time.</summary>
    public const ushort PrefetchCount = 1;

    /// <summary>The content type of the message.</summary>
    public const string ContentType = "application/json";

    /// <summary>
    /// Builds the properties of a message of the queue: persistent, and marked as JSON.
    /// </summary>
    /// <returns>The properties.</returns>
    public static BasicProperties NewMessageProperties()
    {
        return new BasicProperties
        {
            Persistent = true,
            ContentType = ContentType,
        };
    }
}
