using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using OrderService.Authentication.Endpoints;
using OrderService.Common.Api;
using OrderService.Orders.Endpoints;

namespace OrderService;

public static class Endpoints
{
    private static readonly OpenApiSecurityScheme SecurityScheme = new()
    {
        Type = SecuritySchemeType.Http,
        Name = JwtBearerDefaults.AuthenticationScheme,
        Scheme = JwtBearerDefaults.AuthenticationScheme,
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };
    
    public static void MapEndpoints(this WebApplication app)
    {
        var endpoints = app.MapGroup("")
            .WithOpenApi();

        endpoints.MapAuthenticationEndpoints();
        endpoints.MapPostEndpoints();
        // other endpoints could be added here
    }
    
    private static void MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/auth")
            .WithTags("Authentication");
            
        endpoints.MapPublicGroup()
            .MapEndpoint<Signup>()
            .MapEndpoint<Login>();
    }
    
    private static void MapPostEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("/orders")
            .WithTags("Orders");

        endpoints.MapPublicGroup()
            .MapEndpoint<GetOrders>()
            .MapEndpoint<GetOrderById>();

        endpoints.MapAuthorizedGroup()
            .MapEndpoint<CreateOrder>()
            .MapEndpoint<UpdateOrder>()
            .MapEndpoint<DeleteOrder>();
    }
    
    private static RouteGroupBuilder MapPublicGroup(this IEndpointRouteBuilder app, string? prefix = null)
    {
        return app.MapGroup(prefix ?? string.Empty)
            .AllowAnonymous();
    }

    private static RouteGroupBuilder MapAuthorizedGroup(this IEndpointRouteBuilder app, string? prefix = null)
    {
        return app.MapGroup(prefix ?? string.Empty)
            .RequireAuthorization()
            .WithOpenApi(x => new OpenApiOperation(x)
            {
                Security = [new OpenApiSecurityRequirement { [SecurityScheme] = [] }],
            });
    }
    
    private static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app) where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}