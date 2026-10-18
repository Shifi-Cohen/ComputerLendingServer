using ComputerLending.Core.Entities;

namespace ComputerLending.Core.Interfaces;

public interface ITokenService
{
    string CreateToken(User user, DateTime expiresAtUtc);
    DateTime GetExpiresAtUtc();
}
