using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

public interface IPasswordResetTokenStore
{
    Result<PasswordResetToken> Store(PasswordResetToken token);
    Result<PasswordResetToken> FindByToken(string token);
}
