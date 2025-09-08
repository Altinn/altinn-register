﻿using System.Text.Json;
using Altinn.Authorization.ServiceDefaults.MassTransit;
using MassTransit;

namespace Altinn.Register.Tests.UnitTests;

public class CommandBaseTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void TestCommand_RoundTrips_WithCorrelationId()
    {
        var command = new TestCommand
        {
            Foo = "foo",
            Bar = "bar",
        };

        var correlationId = ((CorrelatedBy<Guid>)command).CorrelationId;
        correlationId.Should().NotBeEmpty();

        var json = JsonSerializer.Serialize(command, Options);
        var roundTripped = JsonSerializer.Deserialize<TestCommand>(json, Options);

        roundTripped.Should().BeEquivalentTo(command);
        ((CorrelatedBy<Guid>)roundTripped).CorrelationId.Should().Be(correlationId);
    }

    private sealed record TestCommand
        : CommandBase
    {
        public required string Foo { get; init; }

        public required string Bar { get; init; }
    }
}
