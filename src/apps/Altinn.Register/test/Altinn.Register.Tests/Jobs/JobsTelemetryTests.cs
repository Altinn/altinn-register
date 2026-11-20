using System.Diagnostics.Metrics;
using Altinn.Authorization.ServiceDefaults.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.Jobs;

public class JobsTelemetryTests
{
    [Fact]
    public void LastSuccessTime_IsObservedPerJob_AndRetainedBetweenRuns()
    {
        using var services = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var factory = services.GetRequiredService<IMeterFactory>();
        var meter = factory.Create(JobsTelemetry.Name);
        var observations = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (ReferenceEquals(instrument.Meter, meter) && instrument.Name == "register.jobs.last_success_time")
            {
                instrument.ShouldBeOfType<ObservableGauge<long>>();
                instrument.Unit.ShouldBe("s");
                listener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
        {
            tags.Length.ShouldBe(1);
            tags[0].Key.ShouldBe("job.name");
            observations[(string)tags[0].Value!] = value;
        });
        listener.Start();
        var telemetry = new JobsTelemetry(factory);

        listener.RecordObservableInstruments();
        observations.ShouldBeEmpty();

        telemetry.RecordJobSuccess("first", DateTimeOffset.FromUnixTimeSeconds(100));
        telemetry.RecordJobSuccess("second", DateTimeOffset.FromUnixTimeSeconds(200));
        listener.RecordObservableInstruments();
        observations["first"].ShouldBe(100);
        observations["second"].ShouldBe(200);

        observations.Clear();
        listener.RecordObservableInstruments();
        observations["first"].ShouldBe(100);
        observations["second"].ShouldBe(200);

        telemetry.RecordJobSuccess("first", DateTimeOffset.FromUnixTimeSeconds(300));
        telemetry.RecordJobSuccess("first", DateTimeOffset.FromUnixTimeSeconds(250));
        listener.RecordObservableInstruments();
        observations["first"].ShouldBe(300);
        observations["second"].ShouldBe(200);
    }
}
