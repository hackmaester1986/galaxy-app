namespace GalaxyClassifierApi.Models.Dtos;

public sealed class RandomImagesResponseDto
{
    public List<ImageItemDto> Images { get; set; } = new();
}