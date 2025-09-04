using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;

namespace Altinn.Register.IntegrationTests.TestServices;

internal class TestPublicSigningKeyProvider
    : X509CertificateBasedSigningKeyProvider
{
    private readonly ConcurrentDictionary<string, X509Certificate2> _certificates = new();

    protected override Task<X509Certificate2?> GetCertificate(string issuer, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
