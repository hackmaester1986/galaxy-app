using GalaxyClassifierApi.Models.Dtos;

namespace GalaxyClassifierApi.Services.Interfaces;

public interface IS3ImageService
{
    Task<List<ImageItemDto>> GetRandomImagesAsync(int perPrefix = 4, CancellationToken cancellationToken = default);
    Task<UploadImageResponseDto> UploadImageAsync(IFormFile file, CancellationToken cancellationToken = default);
    bool IsAllowedS3Uri(string s3Uri);
}