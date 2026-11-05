using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lpb.Visualizer;

public partial class Program
{
    internal const int MaximumBodyBytes = 16 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.Strict
    };

    public static async Task Main(string[] args)
    {
        var port = ParsePort(args);
        var builder = WebApplication.CreateBuilder(args);
        // Replace inherited Kestrel endpoint configuration: this debugger never listens on a public interface.
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Configure(new ConfigurationBuilder().Build(), reloadOnChange: false);
            options.ListenLocalhost(port);
            options.Limits.MaxRequestBodySize = MaximumBodyBytes;
        });
        // Console lifecycle messages only. Numerical diagnostics are returned to the owning page, never logged.
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFilter((category, level) => category == "Microsoft.Hosting.Lifetime" && level >= LogLevel.Information);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ExperimentStore>();
        builder.Services.AddHostedService<ExperimentCleanup>();
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            var api = context.Request.Path.StartsWithSegments("/api");
            if (api) context.Response.Headers.CacheControl = "no-store";
            try
            {
                ValidateHost(context.Request.Host.Host);
                if (api)
                {
                    ValidateOrigin(context.Request);
                    context.Items["owner"] = ReadOwner(context.Request);
                    if (context.Request.Method is "POST" or "DELETE")
                    {
                        if (!System.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var type)
                            || !string.Equals(type.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                            || (type.CharSet is not null && !string.Equals(type.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
                            throw new ApiException(415, "protocol", "Use application/json with UTF-8 encoding.");
                        if (context.Request.ContentLength > MaximumBodyBytes)
                            throw new ApiException(413, "protocol", "Request body exceeds 16 KiB.");
                    }
                }
                await next(context);
            }
            catch (InputException exception) { await Error(context, 400, "validation", exception.Message, exception.Errors); }
            catch (JsonException) { await Error(context, 400, "validation", "Provide a complete, valid JSON request with numeric values."); }
            catch (BadHttpRequestException exception) { await Error(context, exception.StatusCode, "protocol", "Invalid or oversized HTTP request."); }
            catch (ApiException exception) { await Error(context, exception.Status, exception.Category, exception.Message); }
            catch (SimulationException exception) { await Error(context, 422, "simulation", exception.Message); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
            catch (Exception) { await Error(context, 500, "application", "An unexpected application failure occurred. Try a new run."); }
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapPost("/api/simulations", async (HttpContext context, ExperimentStore store) =>
        {
            var input = await ReadJson<RunInput>(context);
            DisplaySampler.ValidateCount(input.DisplayPointCount);
            var launch = LaunchAdapter.Convert(input.Launch);
            Begin(store);
            try
            {
                var experiment = Experiment.Run(launch);
                var id = Guid.NewGuid();
                var response = experiment.Display(id, input.DisplayPointCount);
                context.RequestAborted.ThrowIfCancellationRequested();
                store.Put(Owner(context), id, experiment);
                return Results.Json(response);
            }
            finally { store.EndOperation(); }
        });
        app.MapPost("/api/simulations/{id}/display", async (string id, HttpContext context, ExperimentStore store) =>
        {
            var input = await ReadJson<DisplayInput>(context);
            DisplaySampler.ValidateCount(input.DisplayPointCount);
            Begin(store);
            try
            {
                var resultId = ResultId(id);
                var experiment = store.Get(Owner(context), resultId) ?? throw MissingResult();
                var response = experiment.Display(resultId, input.DisplayPointCount);
                context.RequestAborted.ThrowIfCancellationRequested();
                store.Touch(Owner(context), resultId);
                return Results.Json(response);
            }
            finally { store.EndOperation(); }
        });
        app.MapDelete("/api/simulations/{id}", async (string id, HttpContext context, ExperimentStore store) =>
        {
            var body = await ReadJson<JsonElement>(context);
            if (body.ValueKind != JsonValueKind.Object || body.EnumerateObject().Any())
                throw new ApiException(400, "validation", "Deletion requires an empty JSON object.");
            Begin(store);
            try
            {
                if (!store.Delete(Owner(context), ResultId(id))) throw MissingResult();
                return Results.NoContent();
            }
            finally { store.EndOperation(); }
        });
        await app.RunAsync();
    }

    internal static int ParsePort(string[] args)
    {
        // Standard hosting arguments are also used by WebApplicationFactory. Explicit ListenLocalhost
        // overrides URL configuration; no hosting argument may add a non-loopback listener.
        var index = Array.IndexOf(args, "--port");
        if (index < 0 && !args.Any(arg => arg.StartsWith("--port=", StringComparison.Ordinal))) return 5055;
        if (index >= 0 && index + 1 < args.Length && args.Count(arg => arg == "--port") == 1
            && int.TryParse(args[index + 1], out var port) && port is >= 1 and <= 65535)
            return port;
        throw new ArgumentException("Use --port followed by an integer from 1 through 65535. Only loopback binding is supported.");
    }

    private static void ValidateHost(string host)
    {
        if (!string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) && host is not ("127.0.0.1" or "[::1]" or "::1"))
            throw new ApiException(400, "protocol", "Only localhost and loopback Hosts are accepted.");
    }

    private static void ValidateOrigin(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var values)) return;
        if (values.Count != 1 || !Uri.TryCreate(values[0], UriKind.Absolute, out var origin)
            || origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.AbsolutePath != "/"
            || !Uri.TryCreate($"{request.Scheme}://{request.Host}", UriKind.Absolute, out var current)
            || !string.Equals(origin.GetLeftPart(UriPartial.Authority), current.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            throw new ApiException(403, "protocol", "Only same-origin API requests are accepted.");
    }

    private static Guid ReadOwner(HttpRequest request)
    {
        var values = request.Headers["X-Lpb-Tab-Id"];
        if (values.Count != 1 || values[0]?.Length != 36 || !Guid.TryParseExact(values[0], "D", out var owner) || owner == Guid.Empty)
            throw new ApiException(400, "protocol", "X-Lpb-Tab-Id must be one nonempty, 36-character UUID in D format.");
        return owner;
    }

    private static Guid Owner(HttpContext context) => (Guid)context.Items["owner"]!;
    private static Guid ResultId(string id) => Guid.TryParseExact(id, "D", out var result) ? result : throw MissingResult();
    private static ApiException MissingResult() => new(404, "resource", "Experiment no longer available; run again.");
    private static void Begin(ExperimentStore store)
    {
        if (!store.TryBeginOperation()) throw new ApiException(429, "resource", "Another operation is running. Retry when it finishes.");
    }

    private static async Task<T> ReadJson<T>(HttpContext context)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[4096];
        int count;
        while ((count = await context.Request.Body.ReadAsync(bytes, context.RequestAborted)) != 0)
        {
            if (buffer.Length + count > MaximumBodyBytes)
                throw new ApiException(413, "protocol", "Request body exceeds 16 KiB.");
            buffer.Write(bytes, 0, count);
        }
        return JsonSerializer.Deserialize<T>(buffer.ToArray(), JsonOptions)
            ?? throw new ApiException(400, "validation", "A JSON object is required.");
    }

    private static Task Error(HttpContext context, int status, string category, string message, Dictionary<string, string>? fields = null)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new { category, message, fields });
    }

    private sealed class ApiException(int status, string category, string message) : Exception(message)
    {
        public int Status { get; } = status;
        public string Category { get; } = category;
    }
}
