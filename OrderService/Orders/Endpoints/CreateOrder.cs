using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;
using OrderService.Data.Types;
using Serilog;

namespace OrderService.Orders.Endpoints;

public class CreateOrder : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/", Handle)
            .WithSummary("Creates a new Order")
            .WithRequestValidation<Request>();
    }
    
    public record Request(string ProductName, int Quantity, decimal Price);
    public record Response(int Id);
    
    public class CreateOrderValidator : AbstractValidator<Request>
    {
        public CreateOrderValidator()
        {
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

    private static async Task<Created<Response>> Handle(
        Request orderRequest,
        AppDbContext db,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var order = new Order
        {
            ProductName = orderRequest.ProductName,
            Quantity = orderRequest.Quantity,
            Price = orderRequest.Price
        };
    
        Log.Information("Creating a new order for product: {ProductName}", order.ProductName);
        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        await publishEndpoint.Publish(order, cancellationToken);

        Log.Information("Order created with ID: {OrderId} and published to RabbitMQ", order.Id);

        var response = new Response(order.Id);
        
        return TypedResults.Created($"/orders/{order.Id}", response);
    }
}
