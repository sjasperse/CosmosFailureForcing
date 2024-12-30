using CosmosFailureForcing;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Polly;

// load environment variables from the nearest .env file.
DotNetEnv.Env.TraversePath().Load();

var host = Host.CreateDefaultBuilder()
    .ConfigureLogging(x => {
        x.AddSimpleConsole(x =>
        {
            x.SingleLine = true;
        });
    })
    .ConfigureServices((context, services) => {
        services.AddHttpClient();
        services.AddSingleton<CosmosTestingHttpHandler>();

        services.Configure<CosmosHttpOptions>(context.Configuration.GetSection("CosmosHttpOptions"));
        services.AddSingleton(
            p => p.GetRequiredService<IOptions<CosmosHttpOptions>>().Value
        );
    })
    .UseConsoleLifetime()
    .Build();

var cancellationToken = host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");
logger.LogInformation("Initializing...");

// hard coded for local emulator
var cosmosConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==;TrustServerCertificate=true";
var cosmosClientBuilder = new CosmosClientBuilder(
        cosmosConnectionString
    );

var cosmosHttpOptions = host.Services.GetRequiredService<CosmosHttpOptions>();
if (cosmosHttpOptions.UseTestingHandler)
{
    var cosmosHttpHandler = host.Services.GetRequiredService<CosmosTestingHttpHandler>();
    var cosmosHttpClient = new HttpClient(cosmosHttpHandler)
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    
    cosmosClientBuilder
        .WithLimitToEndpoint(true)
        .WithConnectionModeGateway()
        .WithHttpClientFactory(() => cosmosHttpClient)
        .WithRequestTimeout(cosmosHttpOptions.Timeout);
}

var cosmosClient = cosmosClientBuilder.Build();

var database = cosmosClient.GetDatabase("db");
await cosmosClient.CreateDatabaseIfNotExistsAsync(database.Id);

var container = database.GetContainer("main");
await database.CreateContainerIfNotExistsAsync(new ContainerProperties()
{
    Id = container.Id,
    PartitionKeyPath = "/pk"
});


var retryPolicy = Policy
    .Handle<CosmosException>(ex => {
        logger.LogError(ex.Message);
        return true;
    })
    .WaitAndRetryAsync(5, _ => TimeSpan.MinValue);

var attempt = 0;
await retryPolicy.ExecuteAsync(async () =>
{
    attempt++;
    logger.LogInformation($"Querying. Attempt {attempt}...");
    await container.GetItemQueryIterator<JObject>("select top 10 * from c")
        .ToAsyncEnumerable()
        .ToArrayAsync(cancellationToken);
});

attempt = 0;
await retryPolicy.ExecuteAsync(async () =>
{
    attempt++;
    logger.LogInformation($"Upserting. Attempt {attempt}...");
    await container.UpsertItemAsync(new
    {
        id = "testitem",
        pk = "testing"
    }, cancellationToken: cancellationToken);
});

logger.LogInformation("Finished");
