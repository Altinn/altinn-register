using System.Threading.Tasks;

using Altinn.Register.Services.Interfaces;

namespace Altinn.Register.Tests.Mocks
{
    public class AuthorizationWrapperMock : IAuthorization
    {
        public Task<bool?> ValidateSelectedParty(int userId, int partyId)
        {
            bool? isValid = true;

            if (userId == 2)
            {
                isValid = false;
            }

            return Task.FromResult(isValid);
        }
    }
}
