using System.Net.Http.Json;

namespace Gse.Api.Tests;

public static class HttpClientExtensions
{
    // HttpClient has no PatchAsJsonAsync in .NET 8.
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PatchAsync(url, JsonContent.Create(body));
}
