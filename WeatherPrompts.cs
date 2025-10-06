using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace QuickstartWeatherServer.Prompts
{
    // Each method returns a prompt template (static or assembled).
    // If the SDK supports richer prompt objects, replace string with that type.
    [McpServerPromptType]
    public static class WeatherPrompts
    {
        // 1. Simple reusable summary template
        [McpServerPrompt, Description("Generic forecast summarization prompt (2 concise lines).")]
        public static Task<string> ForecastSummaryBasic()
            => Task.FromResult(
@"You are a concise weather assistant.
Given JSON forecast periods, output exactly:
1) One sentence summarizing temperature trend + notable hazards.
2) One short action/safety note (or 'No hazards.').");

        // 2. Parameterized location-aware template
        [McpServerPrompt, Description("Forecast summary prompt specialized for a location name.")]
        public static Task<string> ForecastSummaryForLocation(string location)
            => Task.FromResult(
$@"You are a weather assistant for {location}.
Summarize upcoming forecast periods into:
Line 1: Temp trend + main hazard keywords.
Line 2: Action advice (or 'None.'). Keep each line < 110 chars.");

        // 3. Inject subset of raw forecast JSON (caller supplies JSON string)
        // NOTE: Caller should pre-truncate large JSON; you can add length guard here.
        [McpServerPrompt, Description("Dynamic prompt embedding provided (already trimmed) forecast JSON.")]
        public static Task<string> InlineForecastJson(string location, string trimmedForecastJson)
        {
            // Defensive: hard cap to avoid runaway size
            if (trimmedForecastJson.Length > 8000)
                trimmedForecastJson = trimmedForecastJson.Substring(0, 8000) + "...(truncated)";

            var template =
$@"You are analyzing National Weather Service forecast data for {location}.
Forecast JSON:
```
{trimmedForecastJson}
```
Produce:
1. One-sentence human summary (trend + hazards).
2. Bullet list (max 3) of actionable recommendations (omit if none).";
            return Task.FromResult(template);
        }

        // 4. Few-shot style classification (hazard level)
        [McpServerPrompt, Description("Classify hazard level (LOW/MODERATE/HIGH) from forecast snippets.")]
        public static Task<string> HazardClassifier()
            => Task.FromResult(
@"Task: Classify overall hazard level: LOW, MODERATE, or HIGH.
Criteria:
- HIGH: Severe storms, >3 hazard keywords (e.g., tornado, hurricane, blizzard), extreme temps, life-threatening.
- MODERATE: Notable storms, strong winds, flooding risk, heat index or wind chill concerns.
- LOW: Routine conditions or minor variations.

Output JSON: { ""level"": <LOW|MODERATE|HIGH>, ""rationale"": ""short reason"" }

Examples:
Input: 'Sunny, light wind, highs near 70F.' -> { ""level"": ""LOW"", ""rationale"": ""Calm benign conditions"" }
Input: 'Strong thunderstorms with damaging winds and large hail possible late afternoon.' -> { ""level"": ""MODERATE"", ""rationale"": ""Severe convective risk"" }
Input: 'Blizzard warning with whiteout conditions and wind chills -35F.' -> { ""level"": ""HIGH"", ""rationale"": ""Life-threatening cold & blizzard"" }");

        // 5. Prompt builder pattern (returns JSON describing template + placeholders)
        // Useful if client wants to fill placeholders itself.
        [McpServerPrompt, Description("Machine-readable template descriptor with placeholders.")]
        public static Task<string> DeclarativeTemplate()
        {
            var obj = new
            {
                name = "forecast.composite.summary",
                description = "Two-line concise forecast + advice.",
                placeholders = new[]
                {
                    new { id = "location", description = "Human-readable location name" },
                    new { id = "periods_json", description = "Array of forecast period objects (trimmed)" }
                },
                template =
@"You are a concise weather assistant for {{location}}.
Given forecast periods JSON:
{{periods_json}}
Line1: Temp trend + hazards (≤ 110 chars)
Line2: Action advice or 'None.'"
            };
            return Task.FromResult(JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}