#nullable enable

using Altinn.Register.Core.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace Altinn.Register.Tests.UnitTests;

public class UnitOfWorkTests
{
    private readonly IUnitOfWorkManager _manager;

    public UnitOfWorkTests()
    {
        var impl = new UnitOfWorkManager.Impl(
            participants: [],
            services: []);

        _manager = new UnitOfWorkManager(impl, NullServices.Instance);
    }

    [Fact]
    public async Task GetService_IUnitOfWork_Fallbacks()
    {
        await using var uow = await _manager.CreateAsync();
        uow.GetService<IUnitOfWork>().Should().BeNull();
    }

    [Fact]
    public async Task GetService_IServiceProvider_ReturnsValue()
    {
        await using var uow = await _manager.CreateAsync();
        uow.GetService<IServiceProvider>().Should().NotBeNull();
    }

    [Fact]
    public async Task GetService_IUnitOfWorkHandle_ReturnsValue()
    {
        await using var uow = await _manager.CreateAsync();
        uow.GetService<IUnitOfWorkHandle>().Should().NotBeNull();
    }

    private sealed class NullServices
        : IServiceProvider
    {
        public static NullServices Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
