using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Lpb.Visualizer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;

namespace Lpb.Visualizer.Tests;

public class EndpointTests
{
    private sealed class Factory(TestClock clock) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
        });
    }
    private Factory factory = null!;
    private HttpClient client = null!;
    private TestClock clock = null!;
    private readonly Guid owner = Guid.NewGuid();

    [SetUp]
    public void SetUp()
    {
        clock = new(); factory = new(clock); client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", owner.ToString());
    }

    [TearDown]
    public void TearDown() { client?.Dispose(); factory?.Dispose(); }

    private async Task<ExperimentResponse> Run(LaunchInput? launch = null, int count = 20)
    {
        using var response = await client.PostAsJsonAsync("/api/simulations", new RunInput(launch ?? Fixtures.Default, count));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        Assert.That(response.Headers.Contains("Set-Cookie"), Is.False);
        return (await response.Content.ReadFromJsonAsync<ExperimentResponse>())!;
    }

    [TestCase("/")]
    [TestCase("/app.js")]
    [TestCase("/app-state.js")]
    [TestCase("/styles.css")]
    [TestCase("/vendor/plotly-gl3d-4.1.2.min.js")]
    public async Task StaticAssetsAreAvailable(string path)
    {
        using var response = await client.GetAsync(path);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType, Is.Not.Null);
    }

    [Test]
    public async Task SamplingKeepsExecutedSettingsIdentityAndTerminals()
    {
        var first = await Run();
        using var response = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(300));
        var display = (await response.Content.ReadFromJsonAsync<ExperimentResponse>())!;
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(display.ResultId, Is.EqualTo(first.ResultId));
        Assert.That(display.ExecutedScenario, Is.EqualTo(first.ExecutedScenario));
        Assert.That(display.Landmarks, Is.EqualTo(first.Landmarks));
        Assert.That(display.Measurements, Is.EqualTo(first.Measurements));
        Assert.That(display.Points, Has.Length.EqualTo(300));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
    }

    [Test]
    public async Task FailedNewRunPreservesPreviousResult()
    {
        var first = await Run();
        using var failure = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default with { CenterHeightFeet = -1 }, 20));
        Assert.That(failure.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var display = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(2));
        Assert.That(display.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ReplacedResultAndStaleDeletionCannotAffectNewResult()
    {
        var first = await Run(); var second = await Run(Fixtures.Default with { SpeedMph = 80 });
        using var oldDisplay = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(2));
        using var oldDelete = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/simulations/{first.ResultId}") { Content = JsonContent.Create(new { }) });
        Assert.That(oldDisplay.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(oldDelete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        using var display = await client.PostAsJsonAsync($"/api/simulations/{second.ResultId}/display", new DisplayInput(2));
        Assert.That(display.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DifferentTabCannotReadOrDeleteOwnedResult()
    {
        var first = await Run();
        client.DefaultRequestHeaders.Remove("X-Lpb-Tab-Id"); client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", Guid.NewGuid().ToString());
        using var display = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(2));
        using var deletion = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/simulations/{first.ResultId}") { Content = JsonContent.Create(new { }) });
        Assert.That(display.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(deletion.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ExpiredAndEvictedResultsRequireAnExplicitNewRun()
    {
        var first = await Run(); clock.Advance(TimeSpan.FromMinutes(15));
        using var expired = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(2));
        Assert.That(expired.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(await expired.Content.ReadAsStringAsync(), Does.Contain("run again"));
        first = await Run();
        for (var i = 0; i < 4; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            client.DefaultRequestHeaders.Remove("X-Lpb-Tab-Id"); client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", Guid.NewGuid().ToString());
            await Run();
        }
        client.DefaultRequestHeaders.Remove("X-Lpb-Tab-Id"); client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", owner.ToString());
        using var evicted = await client.PostAsJsonAsync($"/api/simulations/{first.ResultId}/display", new DisplayInput(2));
        Assert.That(evicted.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(factory.Services.GetRequiredService<ExperimentStore>().Count, Is.EqualTo(4));
    }

    [Test]
    public async Task CurrentResultCanBeDeleted()
    {
        var result = await Run();
        using var deletion = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/simulations/{result.ResultId}") { Content = JsonContent.Create(new { }) });
        Assert.That(deletion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        using var display = await client.PostAsJsonAsync($"/api/simulations/{result.ResultId}/display", new DisplayInput(2));
        Assert.That(display.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase(null)] [TestCase("")] [TestCase("not-a-uuid")]
    [TestCase("00000000-0000-0000-0000-000000000000")]
    [TestCase(" 77ecc970-7101-4545-9ee6-67554fb6588e")]
    [TestCase("77ecc970710145459ee667554fb6588e")]
    public async Task InvalidTabKeyRejectsBeforeSimulation(string? key)
    {
        client.DefaultRequestHeaders.Remove("X-Lpb-Tab-Id");
        if (key is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("X-Lpb-Tab-Id", key);
        using var response = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default, 20));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(factory.Services.GetRequiredService<ExperimentStore>().Count, Is.Zero);
    }

    [Test]
    public async Task DuplicateAndOversizedTabKeysAreRejected()
    {
        client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", Guid.NewGuid().ToString());
        using var duplicate = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default, 20));
        Assert.That(duplicate.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        client.DefaultRequestHeaders.Remove("X-Lpb-Tab-Id"); client.DefaultRequestHeaders.Add("X-Lpb-Tab-Id", new string('a', 1000));
        using var oversized = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default, 20));
        Assert.That(oversized.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("http://evil.example")]
    [TestCase("null")]
    [TestCase("http://localhost:9999")]
    [TestCase("http://localhost/path")]
    public async Task DisallowedOriginIsRejectedWithoutCorsHeaders(string origin)
    {
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var response = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default, 20));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(response.Headers.Contains("Access-Control-Allow-Origin"), Is.False);
    }

    [Test]
    public async Task SameOriginIsAcceptedAndForeignHostIsRejected()
    {
        client.DefaultRequestHeaders.Add("Origin", "http://localhost"); await Run();
        client.DefaultRequestHeaders.Host = "evil.example";
        using var response = await client.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("text/plain")]
    [TestCase("application/x-www-form-urlencoded")]
    [TestCase("application/json; charset=utf-16")]
    public async Task StateChangingOperationsRequireIntendedJsonMediaType(string type)
    {
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? "/api/simulations" : $"/api/simulations/{Guid.NewGuid()}");
            request.Content = new StringContent("{}"); request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(type);
            using var response = await client.SendAsync(request);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.UnsupportedMediaType));
        }
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("{\"launch\":{},\"displayPointCount\":20}")]
    [TestCase("{\"displayPointCount\":2.5}")]
    public async Task MalformedOrIncompleteJsonReturnsValidationError(string body)
    {
        using var response = await client.PostAsync("/api/simulations", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
    }

    [Test]
    public async Task OversizedBodyIsRejectedEvenWithoutContentLength()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/simulations") { Content = new StreamingBody(new string(' ', Program.MaximumBodyBytes + 1)) };
        using var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }

    private sealed class StreamingBody : HttpContent
    {
        private readonly string body;
        public StreamingBody(string body)
        {
            this.body = body;
            Headers.ContentType = new("application/json");
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
    }

    [Test]
    public async Task PhysicsFailureAndBudgetRejectionAreDistinctFromValidation()
    {
        using var numerical = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default with { DurationSeconds = double.Epsilon, TimeStepSeconds = double.Epsilon }, 2));
        Assert.That((int)numerical.StatusCode, Is.EqualTo(422));
        Assert.That(await numerical.Content.ReadAsStringAsync(), Does.Contain("Integration step"));
        using var budget = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default with { Mode = "unconstrained", DurationSeconds = 2, TimeStepSeconds = .00001 }, 2));
        Assert.That((int)budget.StatusCode, Is.EqualTo(422));
        Assert.That(await budget.Content.ReadAsStringAsync(), Does.Contain("100000"));
    }

    [Test]
    public async Task OptionalSamplingFailureIsStillSuccessfulAndFiniteJson()
    {
        var response = await Run(Fixtures.Default with { Mode = "unconstrained", SpeedMph = 0, GravityMagnitude = 0, DurationSeconds = 8 * double.Epsilon, TimeStepSeconds = 8 * double.Epsilon }, 300);
        Assert.That(response.SamplingSource, Is.EqualTo("Recorded-sample fallback"));
        Assert.That(response.Points, Has.Length.EqualTo(2));
        Assert.DoesNotThrow(() => JsonDocument.Parse(JsonSerializer.Serialize(response)));
    }

    [Test]
    public async Task BusyOperationRejectsWithoutQueuingOrReplacement()
    {
        var first = await Run();
        var store = factory.Services.GetRequiredService<ExperimentStore>();
        Assert.That(store.TryBeginOperation(), Is.True);
        try
        {
            using var response = await client.PostAsJsonAsync("/api/simulations", new RunInput(Fixtures.Default, 20));
            Assert.That((int)response.StatusCode, Is.EqualTo(429));
            Assert.That(store.Get(owner, first.ResultId), Is.Not.Null);
        }
        finally { store.EndOperation(); }
    }

    [TestCase("0")]
    [TestCase("65536")]
    [TestCase("not-a-port")]
    public void InvalidPortIsRejected(string value) => Assert.Throws<ArgumentException>(() => Program.ParsePort(["--port", value]));
}
