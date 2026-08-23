using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Railway.Toolkit.Demo.Api.Domain;

namespace Railway.Toolkit.Demo.Api.Features.Users.PasswordReset;

public sealed class PasswordResetHandler
{
    private static readonly TimeSpan TokenExpiry = TimeSpan.FromHours(1);

    private readonly IUserStore _userStore;
    private readonly IPasswordResetTokenStore _tokenStore;
    private readonly ILogger<PasswordResetHandler> _logger;

    public PasswordResetHandler(
        IUserStore userStore,
        IPasswordResetTokenStore tokenStore,
        ILogger<PasswordResetHandler> logger)
    {
        _userStore = userStore;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public Result<PasswordResetResponse> Handle(PasswordResetRequest request)
    {
        Result<PasswordResetRequest> validated = Validation.Combine(
            new[]
            {
                Result.Ok(request.Email).NotNullOrEmpty("Email").ToUnit(),
            },
            () => request);

        return validated
            .Bind(req => _userStore.FindByEmail(req.Email))
            .Ensure(
                user => user.IsActive,
                Error.Create("Cannot reset password for an inactive account", "PasswordReset.AccountInactive"))
            .Map(user => new PasswordResetToken
            {
                UserId = user.Id,
                Token = GenerateToken(),
                ExpiresAt = DateTimeOffset.UtcNow.Add(TokenExpiry),
                CreatedAt = DateTimeOffset.UtcNow
            })
            .Bind(_tokenStore.Store)
            .Tap(token => SendMockEmail(request.Email, token))
            .Map(token => new PasswordResetResponse
            {
                Message = "Password reset email sent",
                Token = token.Token,
                ExpiresAt = token.ExpiresAt
            });
    }

    private void SendMockEmail(string email, PasswordResetToken token)
    {
        _logger.LogInformation(
            "[MOCK EMAIL] To: {Email} | Subject: Password Reset | Token: {Token} | Expires: {ExpiresAt}",
            email,
            token.Token,
            token.ExpiresAt);
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
