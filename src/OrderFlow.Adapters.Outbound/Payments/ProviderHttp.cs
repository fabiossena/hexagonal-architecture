using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Adapters.Outbound.Payments;

/// <summary>
/// Small helpers shared by the provider adapters: they turn an HTTP answer into
/// a payload or into a <see cref="PaymentProviderException"/>.
/// </summary>
internal static class ProviderHttp
{
    /// <summary>Sends the request and returns the raw payload.</summary>
    /// <param name="client">HTTP client of the provider.</param>
    /// <param name="request">Request to send.</param>
    /// <param name="providerName">Provider name used in the error messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw response payload.</returns>
    /// <exception cref="PaymentProviderException">The provider is unreachable or answered with an error.</exception>
    public static async Task<string> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        string providerName,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new PaymentProviderException(
                $"{providerName} could not be reached: {exception.Message}",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PaymentProviderException($"{providerName} did not answer in time.", exception);
        }

        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new PaymentProviderException(
                $"{providerName} answered {(int)response.StatusCode} ({response.ReasonPhrase}).",
                payload);
        }

        return payload;
    }

    /// <summary>Creates a JSON request.</summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Relative path.</param>
    /// <param name="body">JSON body.</param>
    /// <returns>The prepared request.</returns>
    public static HttpRequestMessage CreateJsonRequest(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>Creates a form-encoded request (Stripe style).</summary>
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Relative path.</param>
    /// <param name="form">Form fields.</param>
    /// <returns>The prepared request.</returns>
    public static HttpRequestMessage CreateFormRequest(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string>> form)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new FormUrlEncodedContent(form)
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    /// <summary>Parses a JSON payload.</summary>
    /// <param name="payload">Raw payload.</param>
    /// <param name="providerName">Provider name used in the error messages.</param>
    /// <returns>The parsed root element.</returns>
    /// <exception cref="PaymentProviderException">The payload is not valid JSON.</exception>
    public static JsonElement ParseJson(string payload, string providerName)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            throw new PaymentProviderException(
                $"{providerName} returned a payload that is not valid JSON.",
                exception);
        }
    }

    /// <summary>Reads a nested string.</summary>
    /// <param name="root">Root element.</param>
    /// <param name="path">Property path.</param>
    /// <returns>The value, or <c>null</c> when the path does not exist.</returns>
    public static string? GetString(JsonElement root, params string[] path)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var current = root;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
            {
                return null;
            }

            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    /// <summary>Reads a nested integer.</summary>
    /// <param name="root">Root element.</param>
    /// <param name="path">Property path.</param>
    /// <returns>The value, or <c>null</c> when the path does not exist.</returns>
    public static int? GetInt(JsonElement root, params string[] path)
    {
        var raw = GetString(root, path);
        return int.TryParse(raw, out var value) ? value : null;
    }
}
