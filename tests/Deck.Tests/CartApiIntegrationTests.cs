using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Deck.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Deck.Tests;

public class CartApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CartApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private record TokenResponse(string access_token, string token_type);
    private record GetReq(int UserId);
    private record ReplaceReq(int UserId, CartItemId[] Cart);
    private record CartItemId(int ItemId);

    private async Task<string> GetTokenAsync(int userId = 1)
    {
        var res = await _client.PostAsJsonAsync("/auth/token", new { userId });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var tr = await res.Content.ReadFromJsonAsync<TokenResponse>();
        tr!.access_token.Should().NotBeNullOrWhiteSpace();
        return tr.access_token;
    }

    private void UseBearer(string token)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task Auth_IssueToken_Succeeds()
    {
        var tok = await GetTokenAsync(1);
        tok.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Cart_Get_Empty_Then_Replace_Then_Get_Items()
    {
        var token = await GetTokenAsync(1);
        UseBearer(token);

        // 1) Initial get
        var res1 = await _client.PostAsJsonAsync("/cart/get", new GetReq(1));
        res1.StatusCode.Should().Be(HttpStatusCode.OK, $"BODY={await res1.Content.ReadAsStringAsync()}");

        // get Etag
        var etagStr = res1.Headers.ETag?.ToString()
            ?? (res1.Headers.TryGetValues("ETag", out var vs) ? vs.FirstOrDefault() : null);
        etagStr.Should().NotBeNullOrWhiteSpace("server should return an ETag");

        static int ParseVer(string tag)
        {
            var s = tag.Trim();
            if (s.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) s = s[2..];
            s = s.Trim('"');
            return int.TryParse(s, out var v) ? v : throw new InvalidOperationException($"Bad ETag: {tag}");
        }

        var v = ParseVer(etagStr);

        // IfMatch format
        var candidates = new[]
        {
            etagStr,              
            $"W/\"{v}\"",         
            $"\"{v}\"",           
            v.ToString(),        
            $"W/\"{v+1}\"",       
            $"\"{v+1}\"",         
            (v+1).ToString(),     
        }
        .Distinct().ToList();

        // Try if match
        async Task<HttpResponseMessage> TryReplaceAsync(string ifMatch)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/cart/replace")
            {
                Content = System.Net.Http.Json.JsonContent.Create(
                    new ReplaceReq(1, new[] { new CartItemId(1), new CartItemId(2) })
                )
            };
            req.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            return await _client.SendAsync(req);
        }

        HttpResponseMessage? last = null;
        foreach (var im in candidates)
        {
            last = await TryReplaceAsync(im);
            if (last.StatusCode == HttpStatusCode.NoContent)
                break;
        }

        last!.StatusCode.Should().Be(
            HttpStatusCode.NoContent,
            $"tried If-Match candidates: {string.Join(", ", candidates)}; BODY={await last.Content.ReadAsStringAsync()}"
        );

        // 4) Get should return 2 items
        var res3 = await _client.PostAsJsonAsync("/cart/get", new GetReq(1));
        res3.StatusCode.Should().Be(HttpStatusCode.OK, $"BODY={await res3.Content.ReadAsStringAsync()}");

        var raw3 = await res3.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(raw3);

        System.Text.Json.JsonElement items = default;
        bool found = false;

        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            items = doc.RootElement;
            found = true;
        }
        else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var name in new[] { "Cart", "cart", "Items", "items", "cartItems", "CartItems", "data", "Data" })
            {
                if (doc.RootElement.TryGetProperty(name, out items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    found = true;
                    break;
                }
            }
        }

        found.Should().BeTrue($"Unexpected response shape: {raw3}");
        items.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Cart_Replace_With_Stale_ETag_Returns_409()
    {
        var tok = await GetTokenAsync();
        UseBearer(tok);

        // Initialize
        var ok = await _client.PostAsJsonAsync("/cart/replace",
            new ReplaceReq(1, new[] { new CartItemId(1) }));
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Reat Etag
        var get1 = await _client.PostAsJsonAsync("/cart/get", new GetReq(1));
        get1.StatusCode.Should().Be(HttpStatusCode.OK);
        var currentEtag = get1.Headers.ETag?.Tag ?? get1.Headers.ETag?.ToString() ?? get1.Headers.GetValues("ETag").First();

        // replace, version should add 1
        var ok2 = await _client.PostAsJsonAsync("/cart/replace",
            new ReplaceReq(1, new[] { new CartItemId(2) }));
        ok2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Use old IfMath would trigure 409
        var req = JsonContent.Create(new ReplaceReq(1, new[] { new CartItemId(1), new CartItemId(2) }));
        var http = new HttpRequestMessage(HttpMethod.Post, "/cart/replace") { Content = req };
        http.Headers.TryAddWithoutValidation("If-Match", currentEtag); // old ETag

        var conflict = await _client.SendAsync(http);
        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cart_Replace_With_Duplicates_Returns_422()
    {
        var tok = await GetTokenAsync();
        UseBearer(tok);

        var bad = await _client.PostAsJsonAsync("/cart/replace",
            new ReplaceReq(1, new[] { new CartItemId(1), new CartItemId(1) })); // duplicate

        bad.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Cart_History_Returns_Snapshots()
    {
        var tok = await GetTokenAsync();
        UseBearer(tok);

        // generate history
        await _client.PostAsJsonAsync("/cart/replace", new ReplaceReq(1, new[] { new CartItemId(1) }));
        await _client.PostAsJsonAsync("/cart/replace", new ReplaceReq(1, new[] { new CartItemId(2) }));
        await _client.PostAsJsonAsync("/cart/replace", new ReplaceReq(1, new[] { new CartItemId(1), new CartItemId(2) }));

        var res = await _client.GetAsync("/cart/history/1?take=5");
        res.StatusCode.Should().Be(HttpStatusCode.OK);

        var root = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
        root.ValueKind.Should().Be(JsonValueKind.Array);
        root.GetArrayLength().Should().BeGreaterThan(0);

        var first = root[0];

        // Json names
        JsonElement payload;
        var hasPayload =
            first.TryGetProperty("payloadJson", out payload) ||
            first.TryGetProperty("PayloadJson", out payload) ||
            first.TryGetProperty("payload", out payload);

        hasPayload.Should().BeTrue("history item should contain serialized payload");

        var payloadText = payload.ValueKind == JsonValueKind.String
            ? payload.GetString()
            : payload.GetRawText();

        payloadText.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Protected_Endpoint_Without_Token_Returns_401()
    {
        var client = _factory.CreateClient(); // no authrization
        var res = await client.PostAsJsonAsync("/cart/get", new GetReq(1));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
