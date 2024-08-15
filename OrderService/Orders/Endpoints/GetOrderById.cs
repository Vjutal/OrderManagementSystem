using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;

namespace OrderService.Orders.Endpoints;

public class GetOrderById : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/{id}", Handle)
            .WithSummary("Gets an order by id")
            .WithRequestValidation<Request>();
    }
    
    public record Request(int Id);
    
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    public record Response(int Id, string ProductName, int Quantity, decimal Price, DateTime OrderDate );

    private static async Task<Results<Ok<Response>, NotFound>> Handle(
        [AsParameters] Request request,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var post = await database.Orders
            .Where(x => x.Id == request.Id)
            .Select(x => new Response
            (
                x.Id,
                x.ProductName,
                x.Quantity,
                x.Price,
                x.OrderDate
            ))
            .SingleOrDefaultAsync(cancellationToken);

        return post is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(post);
    }
}