using Microsoft.AspNetCore.Mvc;
using Railway.Toolkit.Demo.Api.Features.Users.Login;
using Railway.Toolkit.Demo.Api.Features.Users.Lookup;
using Railway.Toolkit.Demo.Api.Features.Users.Register;

namespace Railway.Toolkit.Demo.Api.Features.Users;

[ApiController]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly RegisterHandler _registerHandler;
    private readonly LoginHandler _loginHandler;
    private readonly LookupHandler _lookupHandler;

    public UsersController(RegisterHandler registerHandler, LoginHandler loginHandler, LookupHandler lookupHandler)
    {
        _registerHandler = registerHandler;
        _loginHandler = loginHandler;
        _lookupHandler = lookupHandler;
    }

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        Result<RegisterResponse> result = _registerHandler.Handle(request);

        return result switch
        {
            Result<RegisterResponse>.Ok ok =>
                CreatedAtAction(nameof(Register), ok.Value),

            Result<RegisterResponse>.Fail { Error.Code: "Validation.Failed" } fail =>
                UnprocessableEntity(fail.Error),

            Result<RegisterResponse>.Fail fail
                when fail.Error.Code is "User.EmailAlreadyExists" or "User.UsernameAlreadyExists" =>
                Conflict(fail.Error),

            _ => StatusCode(500)
        };
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        Result<LoginResponse> result = _loginHandler.Handle(request);

        return result switch
        {
            Result<LoginResponse>.Ok ok =>
                Ok(ok.Value),

            Result<LoginResponse>.Fail { Error.Code: "Validation.Failed" } fail =>
                UnprocessableEntity(fail.Error),

            Result<LoginResponse>.Fail { Error.Code: "Auth.InvalidCredentials" } fail =>
                Unauthorized(fail.Error),

            Result<LoginResponse>.Fail { Error.Code: "Auth.AccountInactive" } fail =>
                StatusCode(403, fail.Error),

            _ => StatusCode(500)
        };
    }

    [HttpGet("{id}")]
    public IActionResult GetById(int id)
    {
        Result<LookupResponse> result = _lookupHandler.Handle(id);

        return result switch
        {
            Result<LookupResponse>.Ok ok =>
                Ok(ok.Value),

            Result<LookupResponse>.Fail { Error.Code: "User.NotFound" } fail =>
                NotFound(fail.Error),

            _ => StatusCode(500)
        };
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        Result<IReadOnlyList<LookupResponse>> result = _lookupHandler.HandleAll();

        return result switch
        {
            Result<IReadOnlyList<LookupResponse>>.Ok ok =>
                Ok(ok.Value),

            _ => StatusCode(500)
        };
    }
}
