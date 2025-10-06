using System;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using QuickstartWeatherServer.Tools; // for extension if needed (HttpClientExt)
using ModelContextProtocol.Server;   // assuming attributes live here

namespace QuickstartWeatherServer.Resources
{
    // Adjust attribute names if your package uses a different one than [McpServerResource]
    [McpServerResourceType]
    public static class WeatherResources
    {
        // Simple JSON resource: raw station metadata
        [McpServerResource, Description("Raw station metadata JSON for the given stationId.")]
        public static async Task<string> Station(HttpClient client, string stationId)
        {
            using var doc = await client.ReadJsonDocumentAsync($"/stations/{stationId}");
            return doc.RootElement.GetRawText();
        }

        // Current gridpoint forecast (entire JSON) by lat/long
        [McpServerResource, Description("Gridpoint forecast JSON for latitude/longitude.")]
        public static async Task<string> ForecastGrid(HttpClient client, double latitude, double longitude)
        {
            // First call points to discover grid endpoint
            using var pointDoc = await client.ReadJsonDocumentAsync($"/points/{latitude:F4},{longitude:F4}");
            var forecastUrl = pointDoc.RootElement.GetProperty("properties").GetProperty("forecast").GetString();
            if (string.IsNullOrEmpty(forecastUrl))
                throw new InvalidOperationException("No forecast URL discovered.");

            // Forecast URL is absolute (api.weather.gov/...) – strip host for the same HttpClient base if needed
            var relative = forecastUrl.Replace("https://api.weather.gov", "", StringComparison.OrdinalIgnoreCase);
            using var forecastDoc = await client.ReadJsonDocumentAsync(relative);
            return forecastDoc.RootElement.GetRawText();
        }

        // A compact synthesized resource: next N forecast periods summarized
        [McpServerResource, Description("Compact textual summary of the next N forecast periods (default 4).")]
        public static async Task<string> ForecastSummary(HttpClient client, double latitude, double longitude, int periods = 4)
        {
            periods = Math.Clamp(periods, 1, 12);

            using var pointDoc = await client.ReadJsonDocumentAsync($"/points/{latitude:F4},{longitude:F4}");
            var forecastUrl = pointDoc.RootElement.GetProperty("properties").GetProperty("forecast").GetString();
            if (string.IsNullOrEmpty(forecastUrl))
                return "No forecast available.";

            var relative = forecastUrl.Replace("https://api.weather.gov", "", StringComparison.OrdinalIgnoreCase);
            using var forecastDoc = await client.ReadJsonDocumentAsync(relative);

            var periodsElem = forecastDoc.RootElement
                .GetProperty("properties")
                .GetProperty("periods")
                .EnumerateArray()
                .Take(periods);

            var sb = new StringBuilder();
            foreach (var p in periodsElem)
            {
                var name = p.GetProperty("name").GetString();
                var shortForecast = p.GetProperty("shortForecast").GetString();
                var temp = p.GetProperty("temperature").GetInt32();
                var unit = p.GetProperty("temperatureUnit").GetString();
                sb.AppendLine($"{name}: {temp}{unit}, {shortForecast}");
            }
            return sb.ToString().TrimEnd();
        }

        // List resource "catalog" (if client wants discoverability)
        [McpServerResource, Description("Lists available weather resource names and brief descriptions.")]
        public static Task<string> Catalog()
        {
            // Keep in sync manually or generate via reflection.
            var catalog = new[]
            {
                new { name = "Station(stationId)", description = "Raw station metadata JSON." },
                new { name = "ForecastGrid(latitude, longitude)", description = "Full gridpoint forecast JSON." },
                new { name = "ForecastSummary(latitude, longitude, periods?)", description = "Compact textual multi-period forecast." },
                new { name = "Catalog()", description = "This list." }
            };
            return Task.FromResult(JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }));
        }

        // Example: binary-ish resource (returns base64) – placeholder to show pattern
        [McpServerResource, Description("Example placeholder binary resource (base64).")]
        public static Task<string> ExampleBinary()
        {
            var bytes = Encoding.UTF8.GetBytes("Demo binary payload");
            var b64 = Convert.ToBase64String(bytes);
            var payload = new
            {
                contentType = "application/octet-stream",
                encoding = "base64",
                data = b64
            };
            return Task.FromResult(JsonSerializer.Serialize(payload));
        }
    }
}