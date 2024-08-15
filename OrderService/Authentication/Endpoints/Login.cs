using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using OrderService.Authentication.Services;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;

namespace OrderService.Authentication.Endpoints;

public class Login : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) => app
        .MapPost("/login", Handle)
        .WithSummary("Logs in a user")
        .WithRequestValidation<Request>();

    public record Request(string Username, string Password);
    public record Response(string Token);
    
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Username).NotEmpty();
            RuleFor(x => x.Password).NotEmpty();
        }
    }

    private static Task<Results<Ok<Response>, UnauthorizedHttpResult>> Handle(
        Request request,
        AppDbContext database,
        Jwt jwt,
        CancellationToken cancellationToken)
    {
        var isAuthorised =
            request.Username.Equals("username")
            && request.Password.Equals("password");
        
        if (!isAuthorised) return Task.FromResult<Results<Ok<Response>, UnauthorizedHttpResult>>(TypedResults.Unauthorized());
        
        var token = jwt.GenerateToken();
        var response = new Response(token);
        return Task.FromResult<Results<Ok<Response>, UnauthorizedHttpResult>>(TypedResults.Ok(response));
    }
}