using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit.Testing;

namespace Altinn.Register.TestUtils.MassTransit;

/// <summary>
/// A conversation helper for the test harness.
/// </summary>
public sealed class TestHarnessConversation
{
    private readonly ITestHarness _harness;
    private readonly Guid _conversationId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHarnessConversation"/> class.
    /// </summary>
    /// <param name="harness">The test harness.</param>
    /// <param name="conversationId">The conversation id.</param>
    internal TestHarnessConversation(ITestHarness harness, Guid conversationId)
    {
        _harness = harness;
        _conversationId = conversationId;
    }

    /// <summary>
    /// Gets the events in the conversation.
    /// </summary>
    public AsyncUnwrappedEnumerable<EventBase, IPublishedMessage<EventBase>> Events
        => new(_harness.Published.SelectAsync<EventBase>(m => m.Context.ConversationId == _conversationId), static m => m.Context.Message);
}
