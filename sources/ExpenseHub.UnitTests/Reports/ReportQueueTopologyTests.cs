using System.Reflection;
using ExpenseHub.Api.Reports;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RabbitMQ.Client;

namespace ExpenseHub.UnitTests.Reports;

/// <summary>
/// Tests of how the report queue is declared and how its messages are written. The web application and the worker declare the
/// queue from the same values, so these tests are the written form of "durable queue and persistent messages". The values of the
/// declaration are read by reflection because they are compile-time constants.
/// </summary>
[TestClass]
public sealed class ReportQueueTopologyTests
{
    /// <summary>The queue has the documented name unless the configuration changes it.</summary>
    [TestMethod]
    public void DefaultQueueName_IsTheDocumentedOne()
    {
        Assert.AreEqual("expensehub.reports", Constant<string>(nameof(ReportQueueTopology.DefaultQueueName)));
        Assert.AreEqual(ReportQueueTopology.DefaultQueueName, new RabbitMqOptions().QueueName);
    }

    /// <summary>The queue survives a restart of the broker.</summary>
    [TestMethod]
    public void Queue_IsDurable()
    {
        Assert.IsTrue(Constant<bool>(nameof(ReportQueueTopology.Durable)));
    }

    /// <summary>The queue is not exclusive, so the worker can reconnect to it after a failure.</summary>
    [TestMethod]
    public void Queue_IsNotExclusive()
    {
        Assert.IsFalse(Constant<bool>(nameof(ReportQueueTopology.Exclusive)));
    }

    /// <summary>The queue is not deleted when the last consumer leaves, so messages wait while the worker is stopped.</summary>
    [TestMethod]
    public void Queue_IsNotDeletedWhenTheWorkerLeaves()
    {
        Assert.IsFalse(Constant<bool>(nameof(ReportQueueTopology.AutoDelete)));
    }

    /// <summary>The worker holds one message at a time, so a message is not taken while another is being processed.</summary>
    [TestMethod]
    public void Worker_HoldsOneMessageAtATime()
    {
        Assert.AreEqual((ushort)1, Constant<ushort>(nameof(ReportQueueTopology.PrefetchCount)));
    }

    /// <summary>Every message is persistent, so the broker writes it to disk before it confirms.</summary>
    [TestMethod]
    public void Message_IsPersistent()
    {
        BasicProperties properties = ReportQueueTopology.NewMessageProperties();

        Assert.IsTrue(properties.Persistent);
        Assert.AreEqual(DeliveryModes.Persistent, properties.DeliveryMode);
    }

    /// <summary>The message is marked as JSON.</summary>
    [TestMethod]
    public void Message_IsJson()
    {
        BasicProperties properties = ReportQueueTopology.NewMessageProperties();

        Assert.AreEqual("application/json", properties.ContentType);
    }

    /// <summary>Each message gets its own properties, so one message can never change another one.</summary>
    [TestMethod]
    public void NewMessageProperties_GivesANewInstanceEachTime()
    {
        Assert.AreNotSame(ReportQueueTopology.NewMessageProperties(), ReportQueueTopology.NewMessageProperties());
    }

    private static T Constant<T>(string name)
    {
        FieldInfo field = typeof(ReportQueueTopology).GetField(name, BindingFlags.Public | BindingFlags.Static)!;

        return (T)field.GetRawConstantValue()!;
    }
}
