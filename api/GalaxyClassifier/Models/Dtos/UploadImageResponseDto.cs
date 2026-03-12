namespace GalaxyClassifierApi.Models.Dtos;

public sealed class UploadImageResponseDto
{
    public string S3Uri { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}