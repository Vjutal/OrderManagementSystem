using System.Text;
using System.Text.Json;
using Asp.Versioning;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Models;
using RabbitMQ.Client;
using Serilog;
using Shared;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Configuration
    .AddEnvironmentVariables(prefix: "DOTNET_")
    .AddEnvironmentVariables(prefix: "ASPNETCORE_")
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true);

// Configure logging
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:l}{NewLine}{Exception}")
    //.WriteTo.File("logs/orderservice-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.Seq("http://localhost:5341")  // Optional, if you want to use Seq for structured logging
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host
    //.ConfigureLogging(builder.Configuration)
    .UseSerilog();

// Configure CORS
builder.Services.AddDefaultCorsPolicy("AllowAllOrigins");

// Configure services
builder.Services.AddDefaultDbContext<OrderDbContext>(builder.Configuration.GetConnectionString("DefaultConnection"));

builder.Services.AddSingleton<RabbitMqPublisher>();

// Configure Health Checks
builder.Services.AddDefaultHealthChecks(
    builder.Configuration.GetConnectionString("DefaultConnection"), 
    builder.Configuration.GetConnectionString("RabbitMQConnection"));

// Configure API Versioning
// builder.Services.AddDefaultApiVersioning();
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddProblemDetails();

builder.Services.AddMassTransit(x =>
{
    x.SetSnakeCaseEndpointNameFormatter();
    
    // x.AddEndpoint<>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration.GetConnectionString("RabbitMQConnection"));
        
        cfg.ReceiveEndpoint("order_queue", e =>
        {
            e.Durable = true;
            e.Exclusive = false;
            e.AutoDelete = false;
        });
        
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Global Exception Handler
app.UseGlobalExceptionHandler();

/*
 Just ensure database is created.
 */
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    try
    {
        db.Database.Migrate();
        Log.Information("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration failed.");
        throw;
    }
}

// Enforce HTTPS
app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAllOrigins");

var versionedApi = app.NewVersionedApi();

// Define endpoints
versionedApi.MapGet("/orders", async (OrderDbContext db) =>
{
    Log.Information("Getting all orders");
    return Results.Ok(await db.Orders.ToListAsync());
});

versionedApi.MapGet("/orders/{id:int}", async (int id, OrderDbContext db) =>
{
    Log.Information("Getting order with ID: {OrderId}", id);
    var order = await db.Orders.FindAsync(id);
    return order is not null ? Results.Ok(order) : Results.NotFound();
});

versionedApi.MapPost("/orders", async (CreateOrderRequest orderRequest, OrderDbContext db, IPublishEndpoint publishEndpoint) =>
{
    var order = new Order
    {
        ProductName = orderRequest.ProductName,
        Quantity = orderRequest.Quantity,
        Price = orderRequest.Price
    };
    
    Log.Information("Creating a new order for product: {ProductName}", order.ProductName);
    db.Orders.Add(order);
    await db.SaveChangesAsync();

    await publishEndpoint.Publish(order);

    Log.Information("Order created with ID: {OrderId} and published to RabbitMQ", order.Id);
    return Results.Created($"/orders/{order.Id}", order);
})
.WithRequestValidation<CreateOrderRequest>();



versionedApi.MapPut("/orders/{id:int}", async (int id, Order updatedOrder, OrderDbContext db) =>
{
    Log.Information("Updating order with ID: {OrderId}", id);
    var order = await db.Orders.FindAsync(id);
    if (order is null)
    {
        Log.Warning("Order with ID: {OrderId} not found", id);
        return Results.NotFound();
    }

    order.ProductName = updatedOrder.ProductName;
    order.Quantity = updatedOrder.Quantity;
    order.Price = updatedOrder.Price;
    order.OrderDate = updatedOrder.OrderDate;

    await db.SaveChangesAsync();
    Log.Information("Order with ID: {OrderId} updated successfully", id);
    return Results.NoContent();
});

versionedApi.MapDelete("/orders/{id:int}", async (int id, OrderDbContext db) =>
{
    Log.Information("Deleting order with ID: {OrderId}", id);
    var order = await db.Orders.FindAsync(id);
    if (order is null)
    {
        Log.Warning("Order with ID: {OrderId} not found", id);
        return Results.NotFound();
    }

    db.Orders.Remove(order);
    await db.SaveChangesAsync();
    Log.Information("Order with ID: {OrderId} deleted successfully", id);
    return Results.NoContent();
});

// Map Health Check Endpoints
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false // Only include the 'liveness' checks
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("db") || check.Tags.Contains("mq") // Include readiness checks for database and RabbitMQ
});

// Run the application
try
{
    Log.Information("Starting OrderService");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderService failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; } = default!;

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options) { }
}

public class RabbitMqPublisher : IDisposable
{
    private readonly IModel _channel;
    private readonly IConnection _connection;
    private readonly int _retryCount;

    public RabbitMqPublisher(IConfiguration configuration)
    {
        var factory = new ConnectionFactory()
        {
            Uri = new Uri(configuration.GetConnectionString("RabbitMQConnection"))
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(queue: "order_queue",
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        
        _retryCount = configuration.GetValue<int>("RabbitMQPublisherSettings:RetryCount");
    }

    public async Task PublishOrderCreated(Order order)
    {
        var message = JsonSerializer.Serialize(order);
        var body = Encoding.UTF8.GetBytes(message);

        var retryCount = _retryCount;
        while (retryCount > 0)
        {
            try
            {
                _channel.BasicPublish(exchange: "",
                    routingKey: "order_queue",
                    basicProperties: null,
                    body: body);
                Log.Information("Order published to RabbitMQ: {OrderId}", order.Id);
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to publish message, retrying...");
                retryCount--;
                if (retryCount == 0)
                {
                    Log.Error(ex, "Failed to publish message after retries: {OrderId}", order.Id);
                    throw;
                }

                await Task.Delay(1000); // Wait before retrying
            }
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}

public record CreateOrderRequest(string ProductName, int Quantity, decimal Price);

public class CreateOrderValidator : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.ProductName)
            .NotEmpty()
            .MinimumLength(3);

        RuleFor(x => x.Quantity)
            .NotEmpty()
            .GreaterThan(0);

        RuleFor(x => x.Price)
            .NotEmpty()
            .GreaterThan((decimal)0.01);
    }
}