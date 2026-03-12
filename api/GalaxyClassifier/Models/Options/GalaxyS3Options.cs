namespace GalaxyClassifierApi.Models.Options;

public sealed class GalaxyS3Options
{
    public string BucketName { get; set; } = string.Empty;
    public List<string> Prefixes { get; set; } = new();
    public string UploadPrefix { get; set; } = string.Empty;
}