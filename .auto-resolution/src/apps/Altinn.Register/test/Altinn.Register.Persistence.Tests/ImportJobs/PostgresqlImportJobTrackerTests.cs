using Altinn.Register.Core.ImportJobs;
using Altinn.Register.Persistence.ImportJobs;
using Altinn.Register.TestUtils;

namespace Altinn.Register.Persistence.Tests.ImportJobs;

public class PostgresqlImportJobTrackerTests
    : DatabaseTestBase
{
    protected override bool SeedData => false;

    private PostgresImportJobTracker? _tracker;

    protected override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        _tracker = GetRequiredService<PostgresImportJobTracker>();
    }

    private IImportJobTracker Tracker
        => _tracker!;

    [Fact]
    public async Task GetQueueStatus_ReturnsDefault_IfJobDoesNotExist()
    {
        var result = await Tracker.GetStatus("test");

        result.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 0,
            SourceMax = 0,
            ProcessedMax = 0,
        });
    }

    [Fact]
    public async Task SourceMax_CanBe_Null()
    {
        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus { SourceMax = null, EnqueuedMax = 10 });
        await _tracker!.ClearCache("test");

        var result = await Tracker.GetStatus("test");
        result.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = null,
            ProcessedMax = 0,
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrackQueueStatus_ReturnsWhetherUpdated(bool clearCacheBeforeEach)
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        Func<string, Task> clearCache
            = clearCacheBeforeEach
            ? id => _tracker!.ClearCache(id)
            : _ => Task.CompletedTask;

        await clearCache("test");
        var result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
        });
        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 4,
            SourceMax = 4,
        });
        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 4,
            SourceMax = 5,
        });
        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
        });
        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 5,
            SourceMax = 5,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 5,
            SourceMax = 6,
        });
        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 5,
            SourceMax = 6,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 6,
            SourceMax = 6,
        });
        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 6,
            SourceMax = 6,
            ProcessedMax = 0,
        });

        await clearCache("test");
        result = await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 6,
            SourceMax = 6,
        });
        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 6,
            SourceMax = 6,
            ProcessedMax = 0,
        });
    }

    [Fact]
    public async Task TrackQueueStatus_UpdatesQueueStatus()
    {
        ImportJobStatus result;

        // create job
        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 100,
        });

        result = await Tracker.GetStatus("test");

        result.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 100,
            ProcessedMax = 0,
        });

        // update job
        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 20,
            SourceMax = 100,
        });

        result = await Tracker.GetStatus("test");

        result.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 20,
            SourceMax = 100,
            ProcessedMax = 0,
        });

        // update job with lower values
        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 15,
            SourceMax = 100,
        });

        result = await Tracker.GetStatus("test");

        result.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 20,
            SourceMax = 100,
            ProcessedMax = 0,
        });
    }

    [Fact]
    public async Task TrackQueueStatus_EnqueuedGreaterThanSource_Throws()
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
        });

        var act = () => Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 11,
            SourceMax = 10,
        });

        var exn = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        exn.Message.Should().Be("Enqueued max must be less than or equal to source max");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrackProcessedStatus_ReturnsWhetherUpdated(bool clearCacheBeforeEach)
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        Func<string, Task> clearCache 
            = clearCacheBeforeEach
            ? id => _tracker!.ClearCache(id)
            : _ => Task.CompletedTask;

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
        });

        await clearCache("test");
        var result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
            ProcessedMax = 5,
        });

        await clearCache("test");
        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 4,
        });

        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
            ProcessedMax = 5,
        });

        await clearCache("test");
        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        result.Updated.Should().BeFalse();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
            ProcessedMax = 5,
        });

        await clearCache("test");
        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 6,
        });

        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
            ProcessedMax = 6,
        });

        await clearCache("test");
        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 10,
        });

        result.Updated.Should().BeTrue();
        result.Status.Should().Be(new ImportJobStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
            ProcessedMax = 10,
        });
    }

    [Fact]
    public async Task TrackProcessedStatus_ProcessedMaxThanEnqueued_Throws()
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
        });

        var act = () => Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 11,
        });

        var exn = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        exn.Message.Should().Be("Processed max must be less than or equal to enqueued max");
    }

    [Fact]
    public async Task TrackProcessedStatus_NonExisting_Throws()
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        var act = () => Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 11,
        });

        var exn = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;
        exn.Message.Should().Be($"Job 'test' does not exist");
    }

    [Fact]
    public async Task Track_NormalLifecycle()
    {
        const string JOB_NAME = "test";

        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 20,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        await Tracker.TrackQueueStatus(JOB_NAME, new ImportJobQueueStatus
        {
            EnqueuedMax = 20,
            SourceMax = 20,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 15,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 20,
        });
    }

    [Fact]
    public async Task Track_NormalLifecycle_SourceMaxNull()
    {
        const string JOB_NAME = "test";

        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = null,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        await Tracker.TrackQueueStatus(JOB_NAME, new ImportJobQueueStatus
        {
            EnqueuedMax = 20,
            SourceMax = null,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 15,
        });

        await Tracker.TrackProcessedStatus(JOB_NAME, new ImportJobProcessingStatus
        {
            ProcessedMax = 20,
        });
    }
}
