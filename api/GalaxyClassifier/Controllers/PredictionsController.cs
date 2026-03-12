using GalaxyClassifierApi.Models.Dtos;
using GalaxyClassifierApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GalaxyClassifierApi.Controllers;

[ApiController]
[Route("api/predictions")]
public sealed class PredictionsController : ControllerBase
{
    private readonly IS3ImageService _s3ImageService;
    private readonly ISageMakerPredictionService _predictionService;

    public PredictionsController(
        IS3ImageService s3ImageService,
        ISageMakerPredictionService predictionService)
    {
        _s3ImageService = s3ImageService;
        _predictionService = predictionService;
    }

    [HttpPost("by-s3-uri")]
    public async Task<ActionResult<PredictResponseDto>> PredictByS3Uri(
        [FromBody] PredictByS3UriRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.S3Uri))
            return BadRequest("S3Uri is required.");

        if (!_s3ImageService.IsAllowedS3Uri(request.S3Uri))
            return BadRequest("The supplied S3 URI is not allowed.");

        var result = await _predictionService.PredictFromS3UriAsync(
            request.S3Uri,
            request.TopK,
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<PredictResponseDto>> UploadAndPredict(
        IFormFile file,
        [FromQuery] int topK,
        CancellationToken cancellationToken)
    {
        if (file == null)
            return BadRequest("File is required.");

        if (topK <= 0)
            topK = 3;

        var upload = await _s3ImageService.UploadImageAsync(file, cancellationToken);

        var result = await _predictionService.PredictFromS3UriAsync(
            upload.S3Uri,
            topK,
            cancellationToken);

        return Ok(result);
    }
}