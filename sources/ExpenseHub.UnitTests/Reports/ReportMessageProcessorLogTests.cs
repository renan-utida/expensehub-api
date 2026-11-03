using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of what the processor logs: every event of a report (start, completion, duplicate, failure, a retry) is one line with the
/// identifier of the report, so a report can be followed in the log, and no line carries the text of an exception, the content of a
/// message, a connection string, a credential, a token or an e-mail address.
/// </summary>
[TestClass]
public sealed class ReportMessageProcessorLogTests
{
    /// <summary>The start and the completion are logged with the identifier and the number of expenses.</summary>
    [TestMethod]
    public async Task Completion_LogsTheStartAndTheCompletionWithTheReportId()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester);

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        Assert.HasCount(2, logger.Lines);
        Assert.IsTrue(logger.Lines.All(line => line.Message.Contains(report.Id.ToString(), StringComparison.Ordinal)));
        Assert.Contains("started", logger.Lines[0].Message);
        Assert.Contains("completed with 3 expenses", logger.Lines[1].Message);
        Assert.AreEqual(LogLevel.Information, logger.Lines[1].Level);
    }

    /// <summary>A resumed report says so in the log.</summary>
    [TestMethod]
    public async Task Resumed_IsLoggedAsResumed()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Processing);
        store.Given(report);

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        Assert.Contains("resumed: True", logger.Lines[0].Message);
    }

    /// <summary>A duplicate is logged with the identifier and the state that was found, and nothing else is logged.</summary>
    [TestMethod]
    public async Task Duplicate_IsLoggedWithTheStateThatWasFound()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        store.Given(report);

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        LogLine line = logger.Lines.Single();
        Assert.Contains(report.Id.ToString(), line.Message);
        Assert.Contains("already Completed", line.Message);
        Assert.Contains("duplicate", line.Message);
    }

    /// <summary>A definitive failure is logged as an error with the identifier and the stable code.</summary>
    [TestMethod]
    public async Task DefinitiveFailure_IsLoggedWithTheCode()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest report = Given(store, string.Empty);

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        LogLine failure = logger.Lines[^1];
        Assert.AreEqual(LogLevel.Error, failure.Level);
        Assert.Contains(report.Id.ToString(), failure.Message);
        Assert.Contains(ReportErrorCodes.InvalidRequest, failure.Message);
    }

    /// <summary>A retry is logged with the identifier and only the type of the failure, never its message.</summary>
    [TestMethod]
    public async Task TransientFailure_IsLoggedWithTheTypeOfTheFailureOnly()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester);
        store.FindFailures = 1;

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        LogLine line = logger.Lines.Single();
        Assert.AreEqual(LogLevel.Warning, line.Level);
        Assert.Contains(report.Id.ToString(), line.Message);
        Assert.Contains("InvalidOperationException", line.Message);
        Assert.Contains("will be tried again", line.Message);
        Assert.DoesNotContain("The database is unavailable", line.Message);
    }

    /// <summary>The text of an unexpected exception never reaches the log, and neither does anything that looks like a connection string.</summary>
    [TestMethod]
    public async Task UnexpectedException_TheMessageOfTheExceptionIsNeverLogged()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository, ListLogger<ReportMessageProcessor> logger) = Create();
        string leaked = "Data Source=" + new string('s', 8) + ";";
        repository.ExpenseReadFailure = new InvalidOperationException(leaked);
        ReportRequest report = Given(store, ReportTestData.Requester);

        await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        string everything = string.Join('\n', logger.Lines.Select(line => line.Message));
        Assert.DoesNotContain(leaked, everything);
        Assert.IsTrue(logger.Lines.All(line => line.Exception is null));
        Assert.Contains("InvalidOperationException", everything);
    }

    /// <summary>The content of a message that is not valid is never logged, because it is untrusted.</summary>
    [TestMethod]
    public async Task InvalidMessage_TheContentIsNeverLogged()
    {
        (ReportMessageProcessor processor, _, _, ListLogger<ReportMessageProcessor> logger) = Create();
        string content = new('z', 12);

        await processor.HandleAsync(Encoding.UTF8.GetBytes($"{{\"{content}\":\"{content}\"}}"), CancellationToken.None);

        LogLine line = logger.Lines.Single();
        Assert.DoesNotContain(content, line.Message);
        Assert.Contains("discarded", line.Message);
    }

    /// <summary>A message for a report that does not exist is logged as an error with its identifier.</summary>
    [TestMethod]
    public async Task UnknownReport_IsLoggedAsAnErrorWithTheReportId()
    {
        (ReportMessageProcessor processor, _, _, ListLogger<ReportMessageProcessor> logger) = Create();
        var id = Guid.NewGuid();

        await processor.HandleAsync(ReportMessage.Serialize(id), CancellationToken.None);

        LogLine line = logger.Lines.Single();
        Assert.AreEqual(LogLevel.Error, line.Level);
        Assert.Contains(id.ToString(), line.Message);
    }

    /// <summary>Nothing the processor logs carries a token, a credential, a header of authorization or an e-mail address.</summary>
    [TestMethod]
    public async Task EveryLine_CarriesNoSecretsAndNoIdentity()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository, ListLogger<ReportMessageProcessor> logger) = Create();
        ReportRequest ok = Given(store, ReportTestData.Requester);
        ReportRequest invalid = Given(store, string.Empty);
        await processor.HandleAsync(ReportMessage.Serialize(ok.Id), CancellationToken.None);
        await processor.HandleAsync(ReportMessage.Serialize(ok.Id), CancellationToken.None);
        await processor.HandleAsync(ReportMessage.Serialize(invalid.Id), CancellationToken.None);
        repository.ExpenseReadFailure = new ReportStorageException("Simulated storage failure.");
        ReportRequest unlucky = Given(store, ReportTestData.Other);
        await processor.HandleAsync(ReportMessage.Serialize(unlucky.Id), CancellationToken.None);

        string everything = string.Join('\n', logger.Lines.Select(line => line.Message));

        Assert.DoesNotContain("Bearer", everything);
        Assert.DoesNotContain("Authorization", everything);
        Assert.DoesNotContain("Data Source", everything);
        Assert.DoesNotContain("@", everything);
        Assert.DoesNotContain(ReportTestData.Requester, everything);
        Assert.DoesNotContain(ReportTestData.Other, everything);
    }

    private static (ReportMessageProcessor Processor, FakeReportProcessingStore Store, FakeReportRepository Repository, ListLogger<ReportMessageProcessor> Logger) Create()
    {
        var repository = new FakeReportRepository();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        var store = new FakeReportProcessingStore();
        var logger = new ListLogger<ReportMessageProcessor>();
        var processor = new ReportMessageProcessor(store, repository, new FixedTimeProvider(ReportTestData.Now), logger);

        return (processor, store, repository, logger);
    }

    private static ReportRequest Given(FakeReportProcessingStore store, string requesterId)
    {
        ReportRequest report = ReportTestData.Report(requesterId, ReportStatus.Pending, ReportScope.Mine);
        store.Given(report);

        return report;
    }
}
