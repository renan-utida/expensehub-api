using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExpenseHub.Api.Deployment;

/// <summary>
/// Writes one line when a process starts, with the name of the process and the commit its image was built from. The line is the
/// evidence that the environment runs the image that was published: it is compared with the tag of the image and with its labels.
/// Nothing else from the configuration is logged.
/// </summary>
public sealed partial class VersionLogHostedService : IHostedService
{
    private readonly string _application;
    private readonly IOptions<BuildInfoOptions> _options;
    private readonly ILogger<VersionLogHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionLogHostedService"/> class.
    /// </summary>
    /// <param name="application">The name of the process, for example <c>web</c> or <c>worker</c>.</param>
    /// <param name="options">The build information.</param>
    /// <param name="logger">The logger.</param>
    public VersionLogHostedService(string application, IOptions<BuildInfoOptions> options, ILogger<VersionLogHostedService> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(application);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _application = application;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        string commit = RunningVersion.Normalize(_options.Value.Commit);

        LogStarted(_application, commit);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "ExpenseHub {Application} started. Image commit: {Commit}.")]
    private partial void LogStarted(string application, string commit);
}
