namespace GalaxyClassifierApi.Models.Dtos;

using System.Text.Json.Serialization;

public sealed class PredictResponseDto
{
    [JsonPropertyName("s3Uri")]
    public string? S3Uri { get; set; }

    [JsonPropertyName("modelname_res")]
    public string? Modelname_res { get; set; }

    [JsonPropertyName("modelname_gal")]
    public string? Modelname_gal { get; set; }

    [JsonPropertyName("predictedLabel_res")]
    public string PredictedLabel_res { get; set; } = string.Empty;

    [JsonPropertyName("confidence_res")]
    public double Confidence_res { get; set; }

    [JsonPropertyName("probabilities_res")]
    public Dictionary<string, double> Probabilities_res { get; set; } = new();

    [JsonPropertyName("topPredictions_res")]
    public List<TopPredictionDto> TopPredictions_res { get; set; } = new();

    [JsonPropertyName("predictedLabel_gal")]
    public string PredictedLabel_gal { get; set; } = string.Empty;

    [JsonPropertyName("confidence_gal")]
    public double Confidence_gal { get; set; }

    [JsonPropertyName("probabilities_gal")]
    public Dictionary<string, double> Probabilities_gal { get; set; } = new();

    [JsonPropertyName("topPredictions_gal")]
    public List<TopPredictionDto> TopPredictions_gal { get; set; } = new();

    [JsonPropertyName("gradCamMimeType")]
    public string? GradCamMimeType { get; set; }

    [JsonPropertyName("gradCamImageBase64")]
    public string? GradCamImageBase64 { get; set; }
}
/*
public sealed class PredictModelsDto
{
    [JsonPropertyName("resnet")]
    public ModelPredictionResultDto? Resnet { get; set; }

    [JsonPropertyName("galaxyCnn")]
    public ModelPredictionResultDto? GalaxyCnn { get; set; }

    [JsonPropertyName("ensemble")]
    public EnsemblePredictionDto? Ensemble { get; set; }
}

public sealed class EnsemblePredictionDto
{
    [JsonPropertyName("stage1")]
    public ModelPredictionResultDto? Stage1 { get; set; }

    [JsonPropertyName("stage2")]
    public ModelPredictionResultDto? Stage2 { get; set; }

    [JsonPropertyName("final")]
    public ModelPredictionResultDto? Final { get; set; }
}

public sealed class ModelPredictionResultDto
{
    [JsonPropertyName("predictedLabel")]
    public string PredictedLabel { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("probabilities")]
    public Dictionary<string, double> Probabilities { get; set; } = new();

    [JsonPropertyName("topPredictions")]
    public List<TopPredictionDto> TopPredictions { get; set; } = new();
}*/