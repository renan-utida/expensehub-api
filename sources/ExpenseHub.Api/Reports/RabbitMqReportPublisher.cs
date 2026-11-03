using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ExpenseHub.Api.Reports;

/// <summary>
/// Publishes the message of a report to the durable queue of the worker and waits for the confirmation of the broker. The
/// message is persistent and the publication is mandatory, so a message that the broker refuses, cannot route or does not confirm
/// in time is reported as not confirmed, and the request is never accepted without a message that was really stored by the
/// broker. The connection is opened on the first publication, so the web application starts even when the broker is down, and it
/// is opened again after a failure, so the application recovers when the broker returns.
/// </summary>
public sealed partial class RabbitMqReportPublisher : IReportPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqReportPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RabbitMqReportPublisher"/> class.
    /// </summary>
    /// <param name="options">The settings of the connection.</param>
    /// <param name="logger">The logger.</param>
    public RabbitMqReportPublisher(IOptions<RabbitMqOptions> options, ILogger<RabbitMqReportPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ReportPublishResult> PublishAsync(Guid reportId, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> invalid = _options.Validate();

        if (invalid.Count > 0)
        {
            LogNotConfigured(reportId, string.Join(", ", invalid));

            throw new ReportPublishException("The message broker settings are missing or invalid.");
        }

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.PublishTimeoutSeconds));
        bool entered = false;

        try
        {
            await _gate.WaitAsync(timeout.Token);
            entered = true;

            IChannel channel = await GetChannelAsync(timeout.Token);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _options.QueueName,
                mandatory: true,
                basicProperties: ReportQueueTopology.NewMessageProperties(),
                body: ReportMessage.Serialize(reportId),
                cancellationToken: timeout.Token);

            LogConfirmed(reportId);

            return ReportPublishResult.Confirmed;
        }
        catch (PublishException)
        {
            LogNotConfirmed(reportId);

            return ReportPublishResult.NotConfirmed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(reportId);

            if (entered)
            {
                await ResetAsync();
            }

            return ReportPublishResult.NotConfirmed;
        }
        catch (RabbitMQClientException exception)
        {
            throw await FailAsync(reportId, exception, entered);
        }
        catch (IOException exception)
        {
            throw await FailAsync(reportId, exception, entered);
        }
        catch (SocketException exception)
        {
            throw await FailAsync(reportId, exception, entered);
        }
        catch (TimeoutException exception)
        {
            throw await FailAsync(reportId, exception, entered);
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();

        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await ResetAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ReportPublishException> FailAsync(Guid reportId, Exception exception, bool entered)
    {
        LogFailed(reportId, exception.GetType().Name);

        if (entered)
        {
            await ResetAsync();
        }

        return new ReportPublishException("The message broker could not be reached.", exception);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true } open)
        {
            return open;
        }

        await ResetAsync();

        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName!,
            Password = _options.Password!,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(_options.PublishTimeoutSeconds),
            ClientProvidedName = "expensehub-web",
        };

        IConnection connection = await factory.CreateConnectionAsync(cancellationToken);

        try
        {
            var channelOptions = new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true);
            IChannel channel = await connection.CreateChannelAsync(channelOptions, cancellationToken);

            await channel.QueueDeclareAsync(
                queue: _options.QueueName,
                durable: ReportQueueTopology.Durable,
                exclusive: ReportQueueTopology.Exclusive,
                autoDelete: ReportQueueTopology.AutoDelete,
                arguments: null,
                cancellationToken: cancellationToken);

            _connection = connection;
            _channel = channel;

            return channel;
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }
    }

    private async Task ResetAsync()
    {
        IChannel? channel = _channel;
        IConnection? connection = _connection;
        _channel = null;
        _connection = null;

        try
        {
            if (channel is not null)
            {
                await channel.DisposeAsync();
            }

            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
        catch (RabbitMQClientException)
        {
            // The broker is already gone; there is nothing left to close.
        }
        catch (IOException)
        {
            // The connection is already broken; there is nothing left to close.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} message confirmed by the broker.")]
    private partial void LogConfirmed(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message was refused or returned by the broker, so it is not confirmed.")]
    private partial void LogNotConfirmed(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message was not confirmed by the broker in time.")]
    private partial void LogTimedOut(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message could not be published ({ErrorType}).")]
    private partial void LogFailed(Guid reportId, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message was not published: the RabbitMq settings are missing or invalid ({Settings}).")]
    private partial void LogNotConfigured(Guid reportId, string settings);
}
