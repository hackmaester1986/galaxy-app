namespace GalaxyClassifierApi.Models.Dtos;

public sealed class ImageItemDto
{
    public string S3Uri { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}