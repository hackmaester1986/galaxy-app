using GalaxyClassifierApi.Models.Dtos;
using GalaxyClassifierApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GalaxyClassifierApi.Controllers;

[ApiController]
[Route("api/images")]
public sealed class ImagesController : ControllerBase
{
    private readonly IS3ImageService _s3ImageService;

    public ImagesController(IS3ImageService s3ImageService)
    {
        _s3ImageService = s3ImageService;
    }

    [HttpGet("random")]
    public async Task<ActionResult<RandomImagesResponseDto>> GetRandomImages(
        CancellationToken cancellationToken)
    {
        var images = await _s3ImageService.GetRandomImagesAsync(4, cancellationToken);
        return Ok(new RandomImagesResponseDto { Images = images });
    }

    [HttpPost("shuffle")]
    public async Task<ActionResult<RandomImagesResponseDto>> ShuffleImages(
        CancellationToken cancellationToken)
    {
        var images = await _s3ImageService.GetRandomImagesAsync(4, cancellationToken);
        return Ok(new RandomImagesResponseDto { Images = images });
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<UploadImageResponseDto>> UploadImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null)
            return BadRequest("File is required.");

        var result = await _s3ImageService.UploadImageAsync(file, cancellationToken);
        return Ok(result);
    }
}