using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Altinn.Register.Core.Ccr;
using Altinn.Register.Core.UnitOfWork;

namespace Altinn.Register.IntegrationTests.Ccr.Xml;

public abstract class CcrXmlUpdateTestBase
    : IntegrationTestBase
{
    protected abstract ValueTask Setup(IUnitOfWork uow, CancellationToken cancellationToken);

    protected abstract ValueTask Verify(IUnitOfWork uow, CancellationToken cancellationToken);

    [StringSyntax(StringSyntaxAttribute.Xml)]
    protected abstract string XmlToApply { get; }

    [Fact]
    public async Task Run()
    {
        await Setup(async (uow, ct) =>
        {
            await Setup(uow, ct);
        });

        await ApplyXml(XmlToApply);

        await Check(async (uow, ct) =>
        {
            await Verify(uow, ct);
        });
    }

    private async Task ApplyXml([StringSyntax(StringSyntaxAttribute.Xml)] string xml)
    {
        var ccrService = GetRequiredService<CcrService>();

        await ccrService.UpdateFromCcr(
            commandId: Guid.CreateVersion7(),
            input: new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(xml)),
            cancellationToken: CancellationToken);
    }
}
