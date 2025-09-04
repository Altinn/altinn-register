using Altinn.Register.Core.ImportJobs;

namespace Altinn.Register.Tests.UnitTests;

public class ImportJobQueueStatusTests
{
    [Fact]
    public void SourceMaxNull()
    {
        var status = new ImportJobQueueStatus
        {
            EnqueuedMax = 42,
            SourceMax = null,
        };

        status.SourceMax.Should().BeNull();
    }

    [Fact]
    public void SourceMaxZero()
    {
        var status = new ImportJobQueueStatus
        {
            EnqueuedMax = 42,
            SourceMax = 0,
        };

        status.SourceMax.Should().Be(0);
    }

    [Fact]
    public void SourceMaxPositive()
    {
        var status = new ImportJobQueueStatus
        {
            EnqueuedMax = 42,
            SourceMax = 42,
        };
        status.SourceMax.Should().Be(42);
    }
}
