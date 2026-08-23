using System.Collections.Concurrent;
using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Infrastructure;

public sealed class PasswordResetTokenStore : IPasswordResetTokenStore
{
    private readonly ConcurrentDictionary<string, PasswordResetToken> _tokens = new();

    public Result<PasswordResetToken> Store(PasswordResetToken token)
    {
        _tokens[token.Token] = token;
        return Result.Ok(token);
    }

    public Result<PasswordResetToken> FindByToken(string token)
    {
        return _tokens.TryGetValue(token, out PasswordResetToken? found)
            ? Result.Ok(found)
            : Result.Fail<PasswordResetToken>("Token not found or expired", "PasswordReset.TokenNotFound");
    }
}
