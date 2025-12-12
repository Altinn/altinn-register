using Altinn.Register.Core.UnitOfWork;
using Altinn.Register.TestUtils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;

namespace Altinn.Register.Persistence.Tests;

public class PostgreSqlFunctionsTests(ITestOutputHelper output)
    : DatabaseTestBase
{
    private const string SequenceName = "register.user_id_seq";

    protected override ITestOutputHelper? TestOutputHelper => output;

    private IUnitOfWorkManager UnitOfWorkManager => GetRequiredService<IUnitOfWorkManager>();

    [Fact]
    public async Task Function_tx_max_safeval_Returns_Max_When_No_Concurrent_Transactions()
    {
        var value = await TxMaxSafeval();
        value.Should().Be(long.MaxValue);
    }

    [Fact]
    public async Task Function_tx_nextval_Returns_New_SequenceNumber()
    {
        List<long> values = new();

        {
            await using var uow1 = await UnitOfWorkManager.CreateAsync();
            await using var uow2 = await UnitOfWorkManager.CreateAsync();
            await using var uow3 = await UnitOfWorkManager.CreateAsync();
            await using var uow4 = await UnitOfWorkManager.CreateAsync();
            await using var uow5 = await UnitOfWorkManager.CreateAsync();

            values.AddRange(await Task.WhenAll([
                TxNextval(uow1),
                TxNextval(uow2),
                TxNextval(uow3),
                TxNextval(uow4),
                TxNextval(uow5),
            ]));

            values.AddRange(await Task.WhenAll([
                TxNextval(uow1),
                TxNextval(uow2),
                TxNextval(uow3),
                TxNextval(uow4),
                TxNextval(uow5),
            ]));

            values.AddRange(await Task.WhenAll([
                TxNextval(uow1),
                TxNextval(uow2),
                TxNextval(uow3),
                TxNextval(uow4),
                TxNextval(uow5),
            ]));
        }

        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Function_tx_max_safeval_Returns_Number_Before_Concurrent_Transactions()
    {
        await using var uow1 = await UnitOfWorkManager.CreateAsync();
        await using var uow2 = await UnitOfWorkManager.CreateAsync();
        await using var uow3 = await UnitOfWorkManager.CreateAsync();
        await using var uow4 = await UnitOfWorkManager.CreateAsync();
        await using var uow5 = await UnitOfWorkManager.CreateAsync();

        var nv1 = await TxNextval(uow1);
        var nv2 = await TxNextval(uow2);
        var nv3 = await TxNextval(uow3);
        var nv4 = await TxNextval(uow4);
        var nv5 = await TxNextval(uow5);

        nv1.Should().BeLessThan(nv2);
        nv2.Should().BeLessThan(nv3);
        nv3.Should().BeLessThan(nv4);
        nv4.Should().BeLessThan(nv5);

        var maxSafeval = await TxMaxSafeval();
        maxSafeval.Should().Be(nv1 - 1);

        await uow2.CommitAsync();

        maxSafeval = await TxMaxSafeval();
        maxSafeval.Should().Be(nv1 - 1);

        await uow1.CommitAsync();

        maxSafeval = await TxMaxSafeval();
        maxSafeval.Should().Be(nv3 - 1);

        await uow5.CommitAsync();
        await uow4.CommitAsync();
        await uow3.CommitAsync();

        maxSafeval = await TxMaxSafeval();
        maxSafeval.Should().Be(long.MaxValue);
    }

    private async Task<long> TxMaxSafeval()
    {
        await using var uow = await UnitOfWorkManager.CreateAsync();

        return await GetSingleValue(uow, /*strpsql*/$"""SELECT register.tx_max_safeval('{SequenceName}') AS max_safeval""");
    }

    private static Task<long> TxNextval(IUnitOfWork uow)
        => GetSingleValue(uow, /*strpsql*/$"""SELECT register.tx_nextval('{SequenceName}') AS nextval""");

    private static async Task<long> GetSingleValue(IUnitOfWork uow, string query)
    {
        var conn = uow.GetRequiredService<NpgsqlConnection>();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;

        await cmd.PrepareAsync();
        await using var reader = await cmd.ExecuteReaderAsync();

        (await reader.ReadAsync()).Should().BeTrue();
        var value = await reader.GetFieldValueAsync<long>(0);
        (await reader.ReadAsync()).Should().BeFalse();

        return value;
    }
}
