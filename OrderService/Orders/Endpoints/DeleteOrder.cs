using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;

namespace OrderService.Orders.Endpoints;

public class DeleteOrder : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) => app
        .MapDelete("/{id}", Handle)
        .WithSummary("Deletes a post")
        .WithRequestValidation<Request>();

    public record Request(int Id);
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    private static async Task<Results<NoContent, NotFound>> Handle(
        [AsParameters] Request request,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var orderToDelete = await database.Orders
            .FindAsync([request.Id], cancellationToken: cancellationToken);

        if (orderToDelete is null) return TypedResults.NotFound();
        
        orderToDelete.IsDeleted = true;
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }
}