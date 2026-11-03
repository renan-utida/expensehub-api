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
/// Tests of the decision between acknowledging, refusing and putting a message back. The table is written by hand. The second
/// group joins the processor to the decision and proves the rule that matters most: the message is acknowledged only after the
/// work is saved, so whatever fails to save never ends in an acknowledgement.
/// </summary>
[TestClass]
public sealed class ReportDeliveryTests
{
    /// <summary>The table of the decisions, written by hand.</summary>
    /// <param name="outcome">How the processing ended.</param>
    /// <param name="expected">What the consumer does with the message.</param>
    [TestMethod]
    [DataRow(ReportMessageOutcome.Completed, ReportDeliveryAction.Acknowledge)]
    [DataRow(ReportMessageOutcome.Duplicate, ReportDeliveryAction.Acknowledge)]
    [DataRow(ReportMessageOutcome.PermanentFailure, ReportDeliveryAction.Acknowledge)]
    [DataRow(ReportMessageOutcome.Discarded, ReportDeliveryAction.RejectWithoutRequeue)]
    [DataRow(ReportMessageOutcome.TransientFailure, ReportDeliveryAction.RequeueAfterDelay)]
    public void For_FollowsTheTable(ReportMessageOutcome outcome, ReportDeliveryAction expected)
    {
        Assert.AreEqual(expected, ReportDelivery.For(outcome));
    }

    /// <summary>An outcome that is not known is never acknowledged: the message comes back.</summary>
    /// <param name="number">A number that is not the value of an outcome.</param>
    [TestMethod]
    [DataRow(-1)]
    [DataRow(5)]
    [DataRow(99)]
    public void For_UnknownOutcome_PutsTheMessageBack(int number)
    {
        Assert.AreEqual(ReportDeliveryAction.RequeueAfterDelay, ReportDelivery.For((ReportMessageOutcome)number));
    }

    /// <summary>A failed write never ends in an acknowledgement: a database that is unavailable at any step puts the message back.</summary>
    /// <param name="step">The step that fails: Find, Start, Read, Complete or Fail.</param>
    [TestMethod]
    [DataRow("Find")]
    [DataRow("Start")]
    [DataRow("Read")]
    [DataRow("Complete")]
    [DataRow("Fail")]
    public async Task UnavailableDatabase_AtAnyStep_PutsTheMessageBack(string step)
    {
        var repository = new FakeReportRepository();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        var store = new FakeReportProcessingStore();
        var processor = new ReportMessageProcessor(store, repository, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportMessageProcessor>.Instance);
        ReportRequest report = ReportTestData.Report(step == "Fail" ? string.Empty : ReportTestData.Requester, ReportStatus.Pending, ReportScope.Mine);
        store.Given(report);
        store.FindFailures = step == "Find" ? 1 : 0;
        store.StartFailures = step == "Start" ? 1 : 0;
        store.CompleteFailures = step == "Complete" ? 1 : 0;
        store.FailFailures = step == "Fail" ? 1 : 0;
        repository.ExpenseReadFailure = step == "Read" ? new ReportStorageException("Simulated storage failure.") : null;

        ReportMessageOutcome outcome = await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.TransientFailure, outcome, step);
        Assert.AreEqual(ReportDeliveryAction.RequeueAfterDelay, ReportDelivery.For(outcome), step);
        Assert.AreNotEqual(ReportStatus.Completed, store.Saved(report.Id).Status, step);
        Assert.AreNotEqual(ReportStatus.Failed, store.Saved(report.Id).Status, step);
    }

    /// <summary>An acknowledgement is given only when the saved state says the work is done: completed, failed or already finished.</summary>
    [TestMethod]
    public async Task Acknowledgement_IsGivenOnlyWhenTheWorkIsSaved()
    {
        var repository = new FakeReportRepository();
        repository.Expenses.AddRange(ReportTestData.ExampleExpenses());
        var store = new FakeReportProcessingStore();
        var processor = new ReportMessageProcessor(store, repository, new FixedTimeProvider(ReportTestData.Now), NullLogger<ReportMessageProcessor>.Instance);
        ReportRequest pending = ReportTestData.Report(ReportTestData.Requester, ReportStatus.Pending, ReportScope.Mine);
        ReportRequest invalid = ReportTestData.Report(string.Empty, ReportStatus.Pending, ReportScope.Mine);
        ReportRequest finished = ReportTestData.CompletedReport(ReportTestData.Requester);
        store.Given(pending);
        store.Given(invalid);
        store.Given(finished);

        foreach (ReportRequest report in new[] { pending, invalid, finished })
        {
            ReportMessageOutcome outcome = await processor.HandleAsync(ReportMessage.Serialize(report.Id), CancellationToken.None);

            Assert.AreEqual(ReportDeliveryAction.Acknowledge, ReportDelivery.For(outcome));
            Assert.IsTrue(store.Saved(report.Id).Status is ReportStatus.Completed or ReportStatus.Failed, $"{report.Status} was acknowledged without being finished");
        }
    }

    /// <summary>A message that cannot be processed and has nothing to save is refused without going back, and a message that is put back is not refused.</summary>
    [TestMethod]
    public async Task UnreadableMessage_IsRefusedWithoutGoingBack()
    {
        var processor = new ReportMessageProcessor(
            new FakeReportProcessingStore(),
            new FakeReportRepository(),
            new FixedTimeProvider(ReportTestData.Now),
            NullLogger<ReportMessageProcessor>.Instance);

        ReportMessageOutcome outcome = await processor.HandleAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.AreEqual(ReportMessageOutcome.Discarded, outcome);
        Assert.AreEqual(ReportDeliveryAction.RejectWithoutRequeue, ReportDelivery.For(outcome));
    }
}
