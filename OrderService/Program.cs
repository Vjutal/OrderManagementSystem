using OrderService;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:l}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting OrderService");
    
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Configuration
        .AddEnvironmentVariables(prefix: "DOTNET_")
        .AddEnvironmentVariables(prefix: "ASPNETCORE_")
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", true);

    builder.AddServices();

    var app = builder.Build();

    await app.Configure();
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderService failed to start.");
}
finally
{
    Log.CloseAndFlush();
}