using System.Security.Claims;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;

namespace OrderService.Orders.Endpoints;

public class UpdateOrder : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPut("/", Handle)
            .WithSummary("Updates a post")
            .WithRequestValidation<Request>();
    }
    
    public record Request(int Id, string ProductName, int Quantity, decimal Price);
    
    public class RequestValidator : AbstractValidator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            
            RuleFor(x => x.ProductName)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(100);

            RuleFor(x => x.Quantity)
                .NotEmpty()
                .GreaterThan(0);

            RuleFor(x => x.Price)
                .NotEmpty()
                .GreaterThan((decimal)0.01);
        }
    }
    
    private static async Task<Results<NoContent, NotFound>> Handle(
        Request request,
        AppDbContext database,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var post = await database.Orders.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        
        if(post is null) return TypedResults.NotFound();
        
        post.ProductName = request.ProductName;
        post.Quantity = request.Quantity;
        post.Price = request.Price;
        post.UpdatedAt = DateTime.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        await publishEndpoint.Publish(new OrderUpdated(post.Id), cancellationToken);
        
        return TypedResults.NoContent();
    }

    public sealed class OrderUpdated(int Id);
}
