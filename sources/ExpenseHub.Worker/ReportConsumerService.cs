using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Reports;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace ExpenseHub.Worker;

/// <summary>
/// The adapter between the report queue and the processor. It connects to the broker, takes one message at a time with manual
/// acknowledgement, hands the body to <see cref="ReportMessageProcessor"/> and does what <see cref="ReportDelivery"/> says with
/// the outcome: acknowledge a message whose work is saved, refuse a message that cannot be processed, or wait and put it back when
/// the work is not saved. It decides nothing by itself: the message is never acknowledged before the processor returns, and a
/// shutdown in the middle leaves the message unacknowledged, so the broker gives it back.
/// </summary>
internal sealed partial class ReportConsumerService : BackgroundService
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ReportConsumerService> _logger;

    public ReportConsumerService(
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ReportConsumerService> logger)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IReadOnlyList<string> invalid = _options.Validate();

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException("The RabbitMq settings are missing or invalid: " + string.Join(", ", invalid) + ".");
        }

        WarnIfTheDatabasePathIsRelative();
        LogStarting(_options.Host, _options.Port, _options.QueueName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RabbitMQClientException exception)
            {
                await WaitBeforeConnectingAgainAsync(exception, stoppingToken);
            }
            catch (IOException exception)
            {
                await WaitBeforeConnectingAgainAsync(exception, stoppingToken);
            }
            catch (SocketException exception)
            {
                await WaitBeforeConnectingAgainAsync(exception, stoppingToken);
            }
            catch (TimeoutException exception)
            {
                await WaitBeforeConnectingAgainAsync(exception, stoppingToken);
            }
        }

        LogStopped();
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName!,
            Password = _options.Password!,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = "expensehub-worker",
        };

        await using IConnection connection = await factory.CreateConnectionAsync(stoppingToken);
        await using IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: ReportQueueTopology.Durable,
            exclusive: ReportQueueTopology.Exclusive,
            autoDelete: ReportQueueTopology.AutoDelete,
            arguments: null,
            cancellationToken: stoppingToken);

        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: ReportQueueTopology.PrefetchCount, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => OnReceivedAsync(channel, delivery, stoppingToken);

        await channel.BasicConsumeAsync(queue: _options.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        LogConsuming(_options.QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        Guid reportId = ReportMessage.TryParse(delivery.Body, out Guid parsed) ? parsed : Guid.Empty;
        LogReceived(reportId, delivery.Redelivered);

        ReportMessageOutcome outcome;

        try
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            ReportMessageProcessor processor = scope.ServiceProvider.GetRequiredService<ReportMessageProcessor>();
            outcome = await processor.HandleAsync(delivery.Body, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: the message is not acknowledged, so the broker gives it back to the next worker.
            return;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Anything unexpected is treated as not saved, so the message comes back instead of staying stuck without an answer.
            LogUnexpected(reportId, exception.GetType().Name);
            outcome = ReportMessageOutcome.TransientFailure;
        }

        try
        {
            await ApplyAsync(channel, delivery, reportId, ReportDelivery.For(outcome), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down while waiting to put the message back: it stays unacknowledged and the broker gives it back.
        }
        catch (AlreadyClosedException exception)
        {
            // The channel closed before the answer: the broker gives the message back, and a repeated delivery is harmless
            // because a finished report is a duplicate.
            LogAnswerLost(reportId, exception.GetType().Name);
        }
    }

    private async Task ApplyAsync(IChannel channel, BasicDeliverEventArgs delivery, Guid reportId, ReportDeliveryAction action, CancellationToken stoppingToken)
    {
        switch (action)
        {
            case ReportDeliveryAction.Acknowledge:
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                LogAcknowledged(reportId);
                break;

            case ReportDeliveryAction.RejectWithoutRequeue:
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken: stoppingToken);
                LogRejected(reportId);
                break;

            default:
                LogWaitingToRequeue(reportId, _options.TransientRetryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_options.TransientRetryDelaySeconds), stoppingToken);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, cancellationToken: stoppingToken);
                break;
        }
    }

    private async Task WaitBeforeConnectingAgainAsync(Exception exception, CancellationToken stoppingToken)
    {
        LogBrokerUnavailable(exception.GetType().Name, _options.TransientRetryDelaySeconds);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.TransientRetryDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down: the loop ends on the next check.
        }
    }

    private void WarnIfTheDatabasePathIsRelative()
    {
        string? connectionString = _configuration.GetConnectionString(PersistenceServiceCollectionExtensions.ConnectionStringName);

        if (connectionString is not null && !Path.IsPathRooted(new SqliteConnectionStringBuilder(connectionString).DataSource))
        {
            LogRelativeDatabasePath();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Report worker starting. Broker {Host}:{Port}, queue {QueueName}.")]
    private partial void LogStarting(string host, int port, string queueName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report worker consuming the queue {QueueName}, one message at a time.")]
    private partial void LogConsuming(string queueName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report worker stopped.")]
    private partial void LogStopped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The database path of the worker is relative. The web application and the worker must use the same file, so set ConnectionStrings__ExpenseHub to an absolute path in both.")]
    private partial void LogRelativeDatabasePath();

    [LoggerMessage(Level = LogLevel.Warning, Message = "The broker is not available ({ErrorType}). Trying again in {Seconds} seconds.")]
    private partial void LogBrokerUnavailable(string errorType, int seconds);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} message received (redelivered: {Redelivered}).")]
    private partial void LogReceived(Guid reportId, bool redelivered);

    [LoggerMessage(Level = LogLevel.Information, Message = "Report {ReportId} message acknowledged after the result was saved or found saved.")]
    private partial void LogAcknowledged(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message refused without being put back: there is nothing to process.")]
    private partial void LogRejected(Guid reportId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} was not saved: the message is not acknowledged and goes back to the queue in {Seconds} seconds.")]
    private partial void LogWaitingToRequeue(Guid reportId, int seconds);

    [LoggerMessage(Level = LogLevel.Error, Message = "Report {ReportId} processing ended with an unexpected failure ({ErrorType}). The message goes back to the queue.")]
    private partial void LogUnexpected(Guid reportId, string errorType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Report {ReportId} message could not be answered because the channel closed ({ErrorType}). The broker gives it back.")]
    private partial void LogAnswerLost(Guid reportId, string errorType);
}
