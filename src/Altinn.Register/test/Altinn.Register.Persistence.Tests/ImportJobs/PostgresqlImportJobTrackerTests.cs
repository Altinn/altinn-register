﻿using Altinn.Register.Core.ImportJobs;
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

    private PostgresImportJobTracker Tracker
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

    [Fact]
    public async Task TrackProcessedStatus_ReturnsWhetherUpdated()
    {
        UpdateTimerRealtime(
            interval: TimeSpan.FromMilliseconds(2),
            stepSize: TimeSpan.FromMilliseconds(10));

        await Tracker.TrackQueueStatus("test", new ImportJobQueueStatus
        {
            EnqueuedMax = 10,
            SourceMax = 10,
        });

        var result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        result.Should().BeTrue();

        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 4,
        });

        result.Should().BeFalse();

        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 5,
        });

        result.Should().BeFalse();

        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 6,
        });

        result.Should().BeTrue();

        result = await Tracker.TrackProcessedStatus("test", new ImportJobProcessingStatus
        {
            ProcessedMax = 10,
        });

        result.Should().BeTrue();
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
}
