namespace ExpenseHub.Api.Reports;

/// <summary>
/// What the consumer does with the message from the broker once the processing has an outcome.
/// </summary>
public enum ReportDeliveryAction
{
    /// <summary>Acknowledge the message: the work is saved, or there is nothing left to do for it.</summary>
    Acknowledge,

    /// <summary>Refuse the message without putting it back: it cannot be processed and there is nothing to save.</summary>
    RejectWithoutRequeue,

    /// <summary>Do not acknowledge: wait a little and put the message back, because the work is not saved.</summary>
    RequeueAfterDelay,
}

/// <summary>
/// The decision between acknowledging, refusing and putting a message back, as a plain function of the outcome, so it is tested
/// without a broker. The consumer only does what this says. A message is acknowledged only when the outcome says the work is saved
/// or that it was already saved; any outcome that is not known puts the message back, so an acknowledgement is never given by default.
/// </summary>
public static class ReportDelivery
{
    /// <summary>
    /// Decides what to do with the message.
    /// </summary>
    /// <param name="outcome">The outcome of the processing.</param>
    /// <returns>The action for the consumer.</returns>
    public static ReportDeliveryAction For(ReportMessageOutcome outcome)
    {
        return outcome switch
        {
            ReportMessageOutcome.Completed => ReportDeliveryAction.Acknowledge,
            ReportMessageOutcome.Duplicate => ReportDeliveryAction.Acknowledge,
            ReportMessageOutcome.PermanentFailure => ReportDeliveryAction.Acknowledge,
            ReportMessageOutcome.Discarded => ReportDeliveryAction.RejectWithoutRequeue,
            _ => ReportDeliveryAction.RequeueAfterDelay,
        };
    }
}
