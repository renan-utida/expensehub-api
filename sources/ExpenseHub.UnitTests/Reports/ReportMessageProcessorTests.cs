using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExpenseHub.Api.Domain.Entities;
using ExpenseHub.Api.Domain.Enums;
using ExpenseHub.Api.Reports;
using ExpenseHub.UnitTests.Expenses;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of what the worker does with a message: the result is the one of the calculator and is saved together with the
/// completion; a finished report is a duplicate that changes nothing, also after a restart; a processing report is resumed; the
/// scope and the requester come from the saved request; a report that can never be calculated fails once with a stable code and
/// a fixed message; and a database that is not available is never taken for a saved state.
/// </summary>
[TestClass]
public sealed class ReportMessageProcessorTests
{
    /// <summary>A pending report of the scope Mine is completed with the result of the calculator: 3 expenses and 175.00 of the requester.</summary>
    [TestMethod]
    public async Task HandleAsync_PendingMine_CompletesWithTheResultOfTheCalculator()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        ReportRequest saved = store.Saved(report.Id);
        Assert.AreEqual(ReportStatus.Completed, saved.Status);
        Assert.AreEqual(ReportTestData.Now, saved.StartedAtUtc);
        Assert.AreEqual(ReportTestData.Now, saved.CompletedAtUtc);
        Assert.IsNull(saved.ErrorCode);
        Assert.IsNull(saved.ErrorMessage);
        Assert.AreEqual(ExpectedJson(ReportScope.Mine, ReportTestData.Requester), saved.ResultJson);
        ReportSummary summary = ReportResultSerializer.Deserialize(saved.ResultJson!);
        Assert.AreEqual(3, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("175.00"), summary.TotalAmount);
    }

    /// <summary>A pending report of the scope Global adds up the expenses of every owner: 4 expenses and 375.00.</summary>
    [TestMethod]
    public async Task HandleAsync_PendingGlobal_CompletesWithTheExpensesOfEveryone()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, "auditor-1", ReportScope.Global);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        string json = store.Saved(report.Id).ResultJson!;
        Assert.AreEqual(ExpectedJson(ReportScope.Global, "auditor-1"), json);
        ReportSummary summary = ReportResultSerializer.Deserialize(json);
        Assert.AreEqual(4, summary.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("375.00"), summary.TotalAmount);
    }

    /// <summary>The scope Mine adds up the requester that was saved: two reports over the same expenses get each its own requester's expenses.</summary>
    [TestMethod]
    public async Task HandleAsync_Mine_UsesTheRequesterThatWasSaved()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest first = Given(store, ReportTestData.Requester, ReportScope.Mine);
        ReportRequest second = Given(store, ReportTestData.Other, ReportScope.Mine);

        await processor.HandleAsync(Body(first), CancellationToken.None);
        await processor.HandleAsync(Body(second), CancellationToken.None);

        Assert.AreEqual(3, ReportResultSerializer.Deserialize(store.Saved(first.Id).ResultJson!).TotalCount);
        ReportSummary others = ReportResultSerializer.Deserialize(store.Saved(second.Id).ResultJson!);
        Assert.AreEqual(1, others.TotalCount);
        Assert.AreEqual(ReportTestData.Dec("200.00"), others.TotalAmount);
    }

    /// <summary>The message cannot widen a report: a requester, a scope or a period in the message are ignored and the saved request decides.</summary>
    [TestMethod]
    public async Task HandleAsync_ExtraFieldsInTheMessage_AreIgnored()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        string body = $"{{\"reportId\":\"{report.Id}\",\"requesterId\":\"{ReportTestData.Other}\",\"scope\":\"Global\",\"fromDate\":\"2000-01-01\",\"toDate\":\"2099-12-31\"}}";

        ReportMessageOutcome outcome = await processor.HandleAsync(Encoding.UTF8.GetBytes(body), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        Assert.AreEqual(ExpectedJson(ReportScope.Mine, ReportTestData.Requester), store.Saved(report.Id).ResultJson);
    }

    /// <summary>Without any expense in the period the report completes with the five states at zero.</summary>
    [TestMethod]
    public async Task HandleAsync_NoExpenses_CompletesWithFiveZeros()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        repository.Expenses.Clear();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        ReportSummary summary = ReportResultSerializer.Deserialize(store.Saved(report.Id).ResultJson!);
        Assert.AreEqual(0, summary.TotalCount);
        Assert.AreEqual(0m, summary.TotalAmount);
        Assert.HasCount(5, summary.ByStatus);
        Assert.IsTrue(summary.ByStatus.All(total => total.Count == 0 && total.Amount == 0m));
    }

    /// <summary>The completion is one write: the state, the instant and the result are saved by a single call, after the start, and nothing is written after it.</summary>
    [TestMethod]
    public async Task HandleAsync_Completion_IsOneWriteWithTheResult()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        await processor.HandleAsync(Body(report), CancellationToken.None);

        string[] expected = ["FindAsync", "TryStartAsync", "TryCompleteAsync"];
        CollectionAssert.AreEqual(expected, store.Calls.ToArray());
        Assert.AreEqual(1, store.CallsOf("TryCompleteAsync"));
        Assert.AreEqual(2, store.AppliedWrites);
        ReportRequest saved = store.Saved(report.Id);
        Assert.IsTrue(saved.Status == ReportStatus.Completed && saved.ResultJson is not null && saved.CompletedAtUtc is not null);
    }

    /// <summary>A report that is already completed is a duplicate: nothing is read, calculated or written, and the saved result and instant stay as they were.</summary>
    [TestMethod]
    public async Task HandleAsync_CompletedReport_IsADuplicateAndChangesNothing()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = ReportTestData.CompletedReport(ReportTestData.Requester);
        report.CompletedAtUtc = ReportTestData.Now.AddDays(-3);
        store.Given(report);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, outcome);
        Assert.AreEqual(0, store.AppliedWrites);
        string[] expected = ["FindAsync"];
        CollectionAssert.AreEqual(expected, store.Calls.ToArray());
        Assert.AreEqual(0, repository.ExpenseReads);
        ReportRequest saved = store.Saved(report.Id);
        Assert.AreEqual(report.ResultJson, saved.ResultJson);
        Assert.AreEqual(report.CompletedAtUtc, saved.CompletedAtUtc);
    }

    /// <summary>A report that already failed is a duplicate too: it is not calculated again and its error is not replaced.</summary>
    [TestMethod]
    public async Task HandleAsync_FailedReport_IsADuplicateAndChangesNothing()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = ReportTestData.FailedReport(ReportTestData.Requester);
        store.Given(report);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, outcome);
        Assert.AreEqual(0, store.AppliedWrites);
        Assert.AreEqual(0, repository.ExpenseReads);
        Assert.AreEqual(ReportStatus.Failed, store.Saved(report.Id).Status);
        Assert.AreEqual("InvalidRequest", store.Saved(report.Id).ErrorCode);
    }

    /// <summary>The same message delivered twice has one effect: the second delivery is a duplicate, and nothing is written or calculated for it.</summary>
    [TestMethod]
    public async Task HandleAsync_SameMessageTwice_HasOneEffect()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);
        string savedJson = store.Saved(report.Id).ResultJson!;
        ReportMessageOutcome second = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, first);
        Assert.AreEqual(ReportMessageOutcome.Duplicate, second);
        Assert.AreEqual(2, store.AppliedWrites);
        Assert.AreEqual(1, repository.ExpenseReads);
        Assert.AreEqual(savedJson, store.Saved(report.Id).ResultJson);
    }

    /// <summary>The protection does not live in the worker: a new processor and a new repository on the same saved state still see a duplicate and do not calculate again, even if the expenses changed.</summary>
    [TestMethod]
    public async Task HandleAsync_AfterARestart_TheCompletedReportIsStillADuplicate()
    {
        var store = new FakeReportProcessingStore();
        (ReportMessageProcessor before, _, _) = Create(store);
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        await before.HandleAsync(Body(report), CancellationToken.None);
        ReportRequest completed = store.Saved(report.Id);

        (ReportMessageProcessor after, _, FakeReportRepository repositoryAfterRestart) = Create(store);
        repositoryAfterRestart.Expenses.Clear();
        ReportMessageOutcome outcome = await after.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, outcome);
        Assert.AreEqual(0, repositoryAfterRestart.ExpenseReads);
        Assert.AreEqual(completed.ResultJson, store.Saved(report.Id).ResultJson);
        Assert.AreEqual(completed.CompletedAtUtc, store.Saved(report.Id).CompletedAtUtc);
        Assert.AreEqual(2, store.AppliedWrites);
    }

    /// <summary>A report that is already processing (a worker stopped halfway) is resumed: it completes, the start is not written again and its instant is kept.</summary>
    [TestMethod]
    public async Task HandleAsync_ProcessingReport_IsResumedAndCompleted()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Processing);
        report.StartedAtUtc = ReportTestData.Now.AddMinutes(-5);
        store.Given(report);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        Assert.AreEqual(0, store.CallsOf("TryStartAsync"));
        ReportRequest saved = store.Saved(report.Id);
        Assert.AreEqual(ReportStatus.Completed, saved.Status);
        Assert.AreEqual(ReportTestData.Now.AddMinutes(-5), saved.StartedAtUtc);
        Assert.AreEqual(ExpectedJson(ReportScope.Mine, ReportTestData.Requester), saved.ResultJson);
    }

    /// <summary>When another worker starts the report first, the start does not apply, the processor looks again, sees it processing and resumes it.</summary>
    [TestMethod]
    public async Task HandleAsync_AnotherWorkerStartedFirst_TheReportIsResumed()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.BeforeStart = saved =>
        {
            saved.Status = ReportStatus.Processing;
            saved.StartedAtUtc = ReportTestData.Now.AddSeconds(-1);
        };

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Completed, outcome);
        Assert.AreEqual(ReportTestData.Now.AddSeconds(-1), store.Saved(report.Id).StartedAtUtc);
    }

    /// <summary>When another worker finishes the report before this one starts, the message is a duplicate and nothing is calculated.</summary>
    [TestMethod]
    public async Task HandleAsync_AnotherWorkerFinishedBeforeTheStart_IsADuplicate()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.BeforeStart = saved =>
        {
            saved.Status = ReportStatus.Completed;
            saved.CompletedAtUtc = ReportTestData.Now.AddSeconds(-1);
            saved.ResultJson = "{}";
        };

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, outcome);
        Assert.AreEqual(0, repository.ExpenseReads);
        Assert.AreEqual("{}", store.Saved(report.Id).ResultJson);
    }

    /// <summary>When another worker completes the report during the calculation, this one does not overwrite it: the completion does not apply and the message is a duplicate.</summary>
    [TestMethod]
    public async Task HandleAsync_AnotherWorkerCompletedDuringTheCalculation_TheSavedResultIsKept()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.BeforeComplete = saved =>
        {
            saved.Status = ReportStatus.Completed;
            saved.CompletedAtUtc = ReportTestData.Now.AddSeconds(-1);
            saved.ResultJson = "{\"saved\":\"by the other worker\"}";
        };

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Duplicate, outcome);
        Assert.AreEqual("{\"saved\":\"by the other worker\"}", store.Saved(report.Id).ResultJson);
        Assert.AreEqual(ReportTestData.Now.AddSeconds(-1), store.Saved(report.Id).CompletedAtUtc);
    }

    /// <summary>A saved request that is not coherent fails once with the stable code InvalidRequest and the fixed message, without a result.</summary>
    /// <param name="requesterId">The requester that was saved.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task HandleAsync_RequestWithoutRequester_FailsWithInvalidRequest(string requesterId)
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, requesterId, ReportScope.Mine);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, outcome);
        AssertFailedWith(store.Saved(report.Id), ReportErrorCodes.InvalidRequest, ReportErrorCodes.InvalidRequestMessage);
    }

    /// <summary>A saved request whose period ends before it starts fails with InvalidRequest.</summary>
    [TestMethod]
    public async Task HandleAsync_InvertedPeriod_FailsWithInvalidRequest()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        report.FromDate = ReportTestData.To;
        report.ToDate = ReportTestData.From;
        store.Given(report);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, outcome);
        AssertFailedWith(store.Saved(report.Id), ReportErrorCodes.InvalidRequest, ReportErrorCodes.InvalidRequestMessage);
    }

    /// <summary>An amount that cannot be added up in cents fails with the stable code ProcessingFailed, and the message is fixed text.</summary>
    [TestMethod]
    public async Task HandleAsync_AmountOutOfRange_FailsWithProcessingFailed()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        repository.Expenses.Add(ReportTestData.Expense(ReportTestData.Requester, ExpenseStatus.Approved, "100000000000000000000", new DateOnly(2026, 9, 10)));
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, outcome);
        AssertFailedWith(store.Saved(report.Id), ReportErrorCodes.ProcessingFailed, ReportErrorCodes.ProcessingFailedMessage);
    }

    /// <summary>An unexpected failure of the calculation fails with ProcessingFailed, and nothing of the exception reaches the saved error.</summary>
    [TestMethod]
    public async Task HandleAsync_UnexpectedException_FailsWithAFixedMessage()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        string leaked = "Data Source=" + new string('s', 8) + ";";
        repository.ExpenseReadFailure = new InvalidOperationException(leaked);
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, outcome);
        ReportRequest saved = store.Saved(report.Id);
        AssertFailedWith(saved, ReportErrorCodes.ProcessingFailed, ReportErrorCodes.ProcessingFailedMessage);
        Assert.DoesNotContain(leaked, saved.ErrorMessage!);
        Assert.DoesNotContain(leaked, saved.ErrorCode!);
    }

    /// <summary>A failure is final: the message of a failed report is a duplicate afterwards, so it does not go round forever.</summary>
    [TestMethod]
    public async Task HandleAsync_AfterADefinitiveFailure_TheNextDeliveryIsADuplicate()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, string.Empty, ReportScope.Mine);

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);
        int writesAfterTheFailure = store.AppliedWrites;
        ReportMessageOutcome second = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, first);
        Assert.AreEqual(ReportMessageOutcome.Duplicate, second);
        Assert.AreEqual(writesAfterTheFailure, store.AppliedWrites);
    }

    /// <summary>A body that is not a valid message is discarded, and the saved state is not touched.</summary>
    /// <param name="body">The text of the body.</param>
    [TestMethod]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("{}")]
    [DataRow("{\"reportId\":\"rel-001\"}")]
    [DataRow("{\"reportId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task HandleAsync_InvalidMessage_IsDiscardedWithoutTouchingTheStore(string body)
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();

        ReportMessageOutcome outcome = await processor.HandleAsync(Encoding.UTF8.GetBytes(body), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Discarded, outcome);
        Assert.IsEmpty(store.Calls);
        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>A message for a report that does not exist is discarded: there is no request to mark, and nothing is written.</summary>
    [TestMethod]
    public async Task HandleAsync_UnknownReport_IsDiscarded()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();

        ReportMessageOutcome outcome = await processor.HandleAsync(ReportMessage.Serialize(Guid.NewGuid()), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Discarded, outcome);
        Assert.AreEqual(0, store.AppliedWrites);
    }

    /// <summary>When the database is unavailable on the first read, the outcome is transient, nothing is written, and a later delivery completes the report.</summary>
    [TestMethod]
    public async Task HandleAsync_StorageUnavailableOnTheFirstRead_IsTransientAndTheNextDeliveryCompletes()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.FindFailures = 1;

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, first);
        Assert.AreEqual(0, store.AppliedWrites);
        Assert.AreEqual(ReportStatus.Pending, store.Saved(report.Id).Status);
        Assert.AreEqual(ReportMessageOutcome.Completed, await processor.HandleAsync(Body(report), CancellationToken.None));
    }

    /// <summary>When the start cannot be saved, the outcome is transient and the report is still pending; a later delivery completes it.</summary>
    [TestMethod]
    public async Task HandleAsync_StorageUnavailableOnTheStart_IsTransientAndTheReportStaysPending()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.StartFailures = 1;

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, first);
        Assert.AreEqual(ReportStatus.Pending, store.Saved(report.Id).Status);
        Assert.AreEqual(ReportMessageOutcome.Completed, await processor.HandleAsync(Body(report), CancellationToken.None));
    }

    /// <summary>When the expenses cannot be read, the outcome is transient: the report is not marked as failed, it stays processing, and a later delivery resumes and completes it.</summary>
    [TestMethod]
    public async Task HandleAsync_ExpensesCannotBeRead_IsTransientNotAFailure()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        repository.ExpenseReadFailure = new ReportStorageException("Simulated storage failure.");

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, first);
        Assert.AreEqual(ReportStatus.Processing, store.Saved(report.Id).Status);
        Assert.IsNull(store.Saved(report.Id).ErrorCode);
        repository.ExpenseReadFailure = null;
        Assert.AreEqual(ReportMessageOutcome.Completed, await processor.HandleAsync(Body(report), CancellationToken.None));
        Assert.AreEqual(ReportStatus.Completed, store.Saved(report.Id).Status);
    }

    /// <summary>When the completion cannot be saved, the outcome is transient and the report is not completed; a later delivery completes it.</summary>
    [TestMethod]
    public async Task HandleAsync_StorageUnavailableOnTheCompletion_IsTransientAndNothingIsCompleted()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.CompleteFailures = 1;

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, first);
        ReportRequest saved = store.Saved(report.Id);
        Assert.AreEqual(ReportStatus.Processing, saved.Status);
        Assert.IsNull(saved.ResultJson);
        Assert.IsNull(saved.CompletedAtUtc);
        Assert.AreEqual(ReportMessageOutcome.Completed, await processor.HandleAsync(Body(report), CancellationToken.None));
    }

    /// <summary>When the failure itself cannot be saved, the outcome is transient: a failure that is not saved is not confirmed, and a later delivery saves it.</summary>
    [TestMethod]
    public async Task HandleAsync_StorageUnavailableWhileSavingAFailure_IsTransientAndTheFailureIsNotConfirmed()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, string.Empty, ReportScope.Mine);
        store.FailFailures = 1;

        ReportMessageOutcome first = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, first);
        Assert.AreEqual(ReportStatus.Processing, store.Saved(report.Id).Status);
        Assert.AreEqual(ReportMessageOutcome.PermanentFailure, await processor.HandleAsync(Body(report), CancellationToken.None));
        Assert.AreEqual(ReportStatus.Failed, store.Saved(report.Id).Status);
    }

    /// <summary>A conditional write that does not apply while the report is still not finished is not a duplicate: nothing is saved, so the message comes back.</summary>
    [TestMethod]
    public async Task HandleAsync_StartDoesNotApplyAndTheReportIsStillPending_IsTransient()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, FakeReportRepository repository) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        store.RefuseStarts = true;

        ReportMessageOutcome outcome = await processor.HandleAsync(Body(report), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, outcome);
        Assert.AreEqual(ReportStatus.Pending, store.Saved(report.Id).Status);
        Assert.AreEqual(0, store.AppliedWrites);
        Assert.AreEqual(0, repository.ExpenseReads);
    }

    /// <summary>A cancelled processing throws instead of returning an outcome, so a message is never acknowledged for a shutdown, and nothing is written.</summary>
    [TestMethod]
    public async Task HandleAsync_Cancelled_ThrowsAndWritesNothing()
    {
        (ReportMessageProcessor processor, FakeReportProcessingStore store, _) = Create();
        ReportRequest report = Given(store, ReportTestData.Requester, ReportScope.Mine);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => processor.HandleAsync(Body(report), cancellation.Token));

        Assert.AreEqual(0, store.AppliedWrites);
        Assert.AreEqual(ReportStatus.Pending, store.Saved(report.Id).Status);
    }

    private static (ReportMessageProcessor Processor, FakeReportProcessingStore Store, FakeReportRepository Repository) Create(FakeReportProcessingStore? store = null)
    {
        var repository = new FakeReportRepository();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        store ??= new FakeReportProcessingStore();
        var processor = new ReportMessageProcessor(store, repository, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportMessageProcessor>.Instance);

        return (processor, store, repository);
    }

    private static ReportRequest Given(FakeReportProcessingStore store, string requesterId, ReportScope scope)
    {
        ReportRequest report = ReportTestData.Report(requesterId, ReportStatus.Pending, scope);
        store.Given(report);

        return report;
    }

    private static byte[] Body(ReportRequest report)
    {
        return ReportMessage.Serialize(report.Id);
    }

    private static string ExpectedJson(ReportScope scope, string requesterId)
    {
        ReportSummary summary = ReportTestData.Calculate(ReportTestData.ExampleExpenses(), ReportTestData.Query(scope, requesterId));

        return ReportResultSerializer.Serialize(summary);
    }

    private static void AssertFailedWith(ReportRequest saved, string code, string message)
    {
        Assert.AreEqual(ReportStatus.Failed, saved.Status);
        Assert.AreEqual(code, saved.ErrorCode);
        Assert.AreEqual(message, saved.ErrorMessage);
        Assert.AreEqual(ReportTestData.Now, saved.CompletedAtUtc);
        Assert.IsNull(saved.ResultJson);
    }
}
