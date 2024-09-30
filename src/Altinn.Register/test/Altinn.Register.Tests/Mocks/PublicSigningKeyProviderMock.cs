using System.Security.Cryptography.X509Certificates;
using Altinn.Common.AccessToken.KeyProvider;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Register.Tests.Mocks;

public class PublicSigningKeyProviderMock 
    : IPublicSigningKeyProvider
{
    public Task<IEnumerable<SecurityKey>> GetSigningKeys(string issuer, CancellationToken cancellationToken = default)
    {
        X509Certificate2 cert = new X509Certificate2($"{issuer}-org.pem");
        SecurityKey key = new X509SecurityKey(cert);

        IEnumerable<SecurityKey> signingKeys = [key];
        return Task.FromResult(signingKeys);
    }
}
