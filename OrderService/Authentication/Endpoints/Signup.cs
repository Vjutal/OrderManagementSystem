using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using OrderService.Authentication.Services;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;

namespace OrderService.Authentication.Endpoints;

public class Signup : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) => app
        .MapPost("/signup", Handle)
        .WithSummary("Creates a new user account")
        .WithRequestValidation<Request>();

    public record Request(string Username, string Password, string Name);
    public record Response(string Token);
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Username).NotEmpty();
            RuleFor(x => x.Password).NotEmpty();
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    private static Task<Ok> Handle(Request request, AppDbContext database, Jwt jwt, CancellationToken cancellationToken)
    {
        // Logic to create new users should be there
        return Task.FromResult(TypedResults.Ok());
    }
}