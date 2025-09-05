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
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHarnessConversation"/> class.
    /// </summary>
    /// <param name="harness">The test harness.</param>
    /// <param name="conversationId">The conversation id.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    internal TestHarnessConversation(ITestHarness harness, Guid conversationId, CancellationToken cancellationToken)
    {
        _harness = harness;
        _conversationId = conversationId;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Gets the events in the conversation.
    /// </summary>
    public AsyncMessageList<EventBase, IPublishedMessage<EventBase>> Events
        => new AsyncMessageList<EventBase, IPublishedMessage<EventBase>, IPublishedMessage>(
            _harness.Published, 
            (IPublishedMessage m) => m.Context.ConversationId == _conversationId, 
            static m => m.Context.Message, 
            _cancellationToken);

    /// <summary>
    /// Gets the commands in the conversation.
    /// </summary>
    public AsyncMessageList<CommandBase, IReceivedMessage<CommandBase>> Commands
        => new AsyncMessageList<CommandBase, IReceivedMessage<CommandBase>, IReceivedMessage>(
            _harness.Consumed, 
            (IReceivedMessage m) => m.Context.ConversationId == _conversationId, 
            static m => m.Context.Message, 
            _cancellationToken);
}
