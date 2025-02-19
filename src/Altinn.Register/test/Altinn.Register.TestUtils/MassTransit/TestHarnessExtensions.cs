﻿using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit.Testing;

namespace Altinn.Register.TestUtils.MassTransit;

/// <summary>
/// Extension methods for <see cref="ITestHarness"/>.
/// </summary>
public static class TestHarnessExtensions
{
    /// <summary>
    /// Gets a conversation helper by it's conversation ID.
    /// </summary>
    /// <param name="harness">The <see cref="ITestHarness"/>.</param>
    /// <param name="guid">The conversation ID.</param>
    /// <returns>A <see cref="TestHarnessConversation"/>.</returns>
    public static TestHarnessConversation Conversation(this ITestHarness harness, Guid guid)
        => new(harness, guid);

    /// <summary>
    /// Gets a conversation helper by the initial command.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="harness">The <see cref="ITestHarness"/>.</param>
    /// <param name="command">The initial command that started the conversation.</param>
    /// <returns>A <see cref="TestHarnessConversation"/>.</returns>
    public static async Task<TestHarnessConversation> Conversation<TCommand>(
        this ITestHarness harness,
        TCommand command)
        where TCommand : CommandBase
    {
        var consumed = await harness.Consumed.SelectAsync<TCommand>(m => m.Context.CorrelationId == command.CommandId).FirstAsync();

        return harness.Conversation(consumed.Context.ConversationId!.Value);
    }
}
