using System.Text;
using System.Text.Json;
using Dotnet8OutputCachingTest;
using Microsoft.AspNetCore.OutputCaching;

var builder = WebApplication.CreateBuilder(args);

// docker run -d --name redis-stack-server -p6379:6379 redis/redis-stack-server
builder.Services.AddOutputCache().AddStackExchangeRedisOutputCache(x =>
{
    x.InstanceName = "redis-dotnet-api";
    x.Configuration = "localhost:6379";
});
var app = builder.Build();
app.UseOutputCache();

app.MapGet("/", async () =>
{
    await Task.Delay(2000);
    return "Hello World!";
}).CacheOutput(x => x.Expire(TimeSpan.FromSeconds(10)));

app.MapGet("/chuck-noris-output-cache", async () =>
{
    // Connect to a random API
    using var client = new HttpClient();
    var response = await client.GetAsync("https://api.chucknorris.io/jokes/random");
    var content = await response.Content.ReadAsStringAsync();
    // Deserialize the response
    var joke = JsonSerializer.Deserialize<ChuckNorisJoke>(content);
    if (joke is null)
        return Results.NotFound("No joke found");
    return Results.Ok(joke.value);
}).CacheOutput(x => x.Expire(TimeSpan.FromSeconds(10)));

app.MapGet("/chuck-noris-httpclient-cache", async (IOutputCacheStore cache) =>
{
    var cacheKey = "ChuckNorrisJoke"; // Define a unique cache key
    
    // Try to get the cached response from Memory cache
    byte[]? result = await cache.GetAsync(cacheKey, CancellationToken.None);
    if (result is not null)
    {
        var cachedJoke = Encoding.UTF8.GetString(result); // Deserialize the response
        return Results.Ok(cachedJoke);
    }
    
    // Connect to the API if not found in the cache
    using var client = new HttpClient();
    var response = await client.GetAsync("https://api.chucknorris.io/jokes/random");
    var content = await response.Content.ReadAsStringAsync();
    var joke = JsonSerializer.Deserialize<ChuckNorisJoke>(content);
    if (joke is null)
        return Results.NotFound("No joke found");
    
    // Store the response in the cache for a certain duration (e.g., 1 hour)
    await cache.SetAsync(cacheKey, 
        Encoding.UTF8.GetBytes(joke.value), 
        null, 
        TimeSpan.FromSeconds(10), 
        CancellationToken.None);
    
    return Results.Ok(joke.value);
});

app.Run();
