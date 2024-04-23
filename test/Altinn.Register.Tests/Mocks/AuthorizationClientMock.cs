using System.Threading;
using System.Threading.Tasks;

using Altinn.Register.Services.Interfaces;

namespace Altinn.Register.Tests.Mocks
{
    public class AuthorizationClientMock : IAuthorizationClient
    {
        public Task<bool?> ValidateSelectedParty(int userId, int partyId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool? isValid = true;

            if (userId == 2)
            {
                isValid = false;
            }

            return Task.FromResult(isValid);
        }
    }
}
