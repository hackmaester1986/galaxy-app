using System.Text.Json.Serialization;

namespace GalaxyClassifierApi.Models.Dtos;

public sealed class TopPredictionDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("probability")]
    public double Probability { get; set; }
}