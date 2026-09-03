using System;

using Newtonsoft.Json;

namespace DTS.Common;

internal sealed class SnapshotCreateOptions
(
    string appName,
    string cube,
    string entity,
    string scenario,
    string timeFilter
)
{
    [JsonProperty("App")] public string AppName { get; } = appName;
    [JsonProperty("Cb")] public string Cube { get; } = cube;
    [JsonProperty("E")] public string Entity { get; } = entity;
    [JsonProperty("S")] public string Scenario { get; } = scenario;
    [JsonProperty("T")] public string TimeFilter { get; } = timeFilter;

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.None);

    public string BuildSnapshotName() =>
        $"{AppName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
}