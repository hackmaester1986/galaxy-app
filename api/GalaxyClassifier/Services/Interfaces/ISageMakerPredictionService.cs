using GalaxyClassifierApi.Models.Dtos;

namespace GalaxyClassifierApi.Services.Interfaces;

public interface ISageMakerPredictionService
{
    Task<PredictResponseDto> PredictFromS3UriAsync(string s3Uri, int topK = 3, CancellationToken cancellationToken = default);
}