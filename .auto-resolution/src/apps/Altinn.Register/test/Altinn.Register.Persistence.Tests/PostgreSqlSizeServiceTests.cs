using Altinn.Register.Core.Utils;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlSizeServiceTests
    : DatabaseTestBase
{
    private PostgreSqlSizeService Service => GetRequiredService<PostgreSqlSizeService>();

    protected override ValueTask ConfigureServices(IServiceCollection services)
    {
        services.TryAddSingleton<PostgreSqlSizeService>();
        
        return base.ConfigureServices(services);
    }

    [Fact]
    public async Task DatabaseHasSize()
    {
        var isEmpty = await Service.IsDatabaseSmallerThan(ByteSize.Byte);
        isEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task DatabaseIsNotHuge()
    {
        var smallerThan = await Service.IsDatabaseSmallerThan(ByteSize.FromGibibytes(1));
        smallerThan.Should().BeTrue();
    }

    [Fact]
    public async Task DoesNotThrowOnNonExistingTable()
    {
        var isZero = await Service.IsTableSmallerThan("fake_schema", "no_table", ByteSize.Byte);
        isZero.Should().BeTrue();
    }
}
