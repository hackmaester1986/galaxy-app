namespace GalaxyClassifierApi.Models.Dtos;

public sealed class PredictByS3UriRequest
{
    public string S3Uri { get; set; } = string.Empty;
    public int TopK { get; set; } = 3;
}