using OrderService.Common.Api;
using OrderService.Common.Api.Extensions;
using OrderService.Common.Requests;

namespace OrderService.Orders.Endpoints;

public class GetOrders : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/", Handle)
            .WithSummary("Gets all orders")
            .WithRequestValidation<Request>();
    }
    
    public record Request(int? Page, int? PageSize) : IPagedRequest;

    public class RequestValidator : PagedRequestValidator<Request>;

    public record Response(int Id, string ProductName, int Quantity, decimal Price, DateTime OrderDate );

    private static async Task<PagedList<Response>> Handle(
        [AsParameters] Request request,
        AppDbContext database,
        CancellationToken cancellationToken)
    {
        var post = await database.Orders
            .Select(x => new Response
            (
                x.Id,
                x.ProductName,
                x.Quantity,
                x.Price,
                x.OrderDate
            ))
            .ToPagedListAsync(request, cancellationToken);

        return post;
    }
}
