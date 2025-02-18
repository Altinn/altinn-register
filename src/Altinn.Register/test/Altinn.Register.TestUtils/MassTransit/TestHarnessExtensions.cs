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
}
