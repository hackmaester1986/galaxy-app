using Amazon.S3;
using Amazon.S3.Model;
using GalaxyClassifierApi.Models.Dtos;
using GalaxyClassifierApi.Models.Options;
using GalaxyClassifierApi.Services.Interfaces;
using Microsoft.Extensions.Options;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
namespace GalaxyClassifierApi.Services;

public sealed class S3ImageService : IS3ImageService
{
    private readonly IAmazonS3 _s3;
    private readonly GalaxyS3Options _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3ImageService> _logger;
    private readonly HashSet<string> _allowedExtensions;

    public S3ImageService(
        IAmazonS3 s3,
        IOptions<GalaxyS3Options> options,
        IConfiguration configuration,
        ILogger<S3ImageService> logger)
    {
        _s3 = s3;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;

        var configuredExtensions = _configuration
            .GetSection("AllowedImageExtensions")
            .Get<string[]>() ?? [".jpg", ".jpeg", ".png", ".webp"];

        _allowedExtensions = configuredExtensions
            .Select(x => x.ToLowerInvariant())
            .ToHashSet();
    }

    public async Task<List<ImageItemDto>> GetRandomImagesAsync(
        int perPrefix = 4,
        CancellationToken cancellationToken = default)
    {
        var manifest = await GetImageManifestAsync(cancellationToken);
        var results = new List<ImageItemDto>();

        foreach (var prefix in _options.Prefixes)
        {
            if (!manifest.TryGetValue(prefix, out var keys))
                continue;

            var sampled = keys
                .Where(IsAllowedImageKey)
                .OrderBy(_ => Random.Shared.Next())
                .Take(perPrefix)
                .Select(key => new ImageItemDto
                {
                    Bucket = _options.BucketName,
                    Key = key,
                    Prefix = prefix,
                    FileName = Path.GetFileName(key),
                    S3Uri = $"s3://{_options.BucketName}/{key}",
                    ImageUrl = GetPreSignedImageUrl(key)
                });

            results.AddRange(sampled);
        }

        return results
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
    }



    public async Task<UploadImageResponseDto> UploadImageAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new InvalidOperationException("No file was provided.");

        const long maxBytes = 10 * 1024 * 1024; // 10 MB
        if (file.Length > maxBytes)
            throw new InvalidOperationException("File is too large.");

        var originalExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(originalExtension))
            throw new InvalidOperationException($"Unsupported image extension: {originalExtension}");

        var baseName = Path.GetFileNameWithoutExtension(file.FileName);
        baseName = SanitizeFileName(baseName);

        var outputExtension = ".jpg";
        var uniqueName = $"{baseName}-{Guid.NewGuid():N}{outputExtension}";
        var key = $"{_options.UploadPrefix.TrimEnd('/')}/{uniqueName}";

        await using var inputStream = file.OpenReadStream();

        try
        {
            using var image = await Image.LoadAsync(inputStream, cancellationToken);

            if (image.Width < 32 || image.Height < 32)
                throw new InvalidOperationException("Image is too small.");

            if (image.Width > 5000 || image.Height > 5000)
                throw new InvalidOperationException("Image dimensions are too large.");

            image.Metadata.ExifProfile = null;
            image.Metadata.IccProfile = null;
            image.Metadata.XmpProfile = null;


            await using var outputStream = new MemoryStream();

            var encoder = new JpegEncoder
            {
                Quality = 90
            };

            await image.SaveAsJpegAsync(outputStream, encoder, cancellationToken);
            outputStream.Position = 0;

            var request = new PutObjectRequest
            {
                BucketName = _options.BucketName,
                Key = key,
                InputStream = outputStream,
                ContentType = "image/jpeg"
            };

            await _s3.PutObjectAsync(request, cancellationToken);

            return new UploadImageResponseDto
            {
                Bucket = _options.BucketName,
                Key = key,
                FileName = uniqueName,
                S3Uri = $"s3://{_options.BucketName}/{key}"
            };
        }
        catch (UnknownImageFormatException)
        {
            throw new InvalidOperationException("The uploaded file is not a valid supported image.");
        }
        catch (InvalidImageContentException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Unable to process the uploaded image.");
        }
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "image";

        var cleaned = new string(input
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(cleaned) ? "image" : cleaned;
    }

    private sealed class InvalidImageContentException : Exception
    {
        public InvalidImageContentException(string message) : base(message) { }
    }

    public bool IsAllowedS3Uri(string s3Uri)
    {
        if (string.IsNullOrWhiteSpace(s3Uri) || !s3Uri.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            return false;

        var withoutScheme = s3Uri["s3://".Length..];
        var slashIndex = withoutScheme.IndexOf('/');

        if (slashIndex <= 0)
            return false;

        var bucket = withoutScheme[..slashIndex];
        var key = withoutScheme[(slashIndex + 1)..];

        if (!bucket.Equals(_options.BucketName, StringComparison.Ordinal))
            return false;

        return _options.Prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal))
               || key.StartsWith(_options.UploadPrefix, StringComparison.Ordinal);
    }

    private string GetPreSignedImageUrl(string key, int expiresInMinutes = 60)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddMinutes(expiresInMinutes)
        };

        return _s3.GetPreSignedURL(request);
    }


    private async Task<Dictionary<string, List<string>>> GetImageManifestAsync(
        CancellationToken cancellationToken)
    {
        var request = new GetObjectRequest
        {
            BucketName = _options.BucketName,
            Key = "galaxy-classifier/manifests/image-manifest.json"
        };

        using var response = await _s3.GetObjectAsync(request, cancellationToken);
        using var reader = new StreamReader(response.ResponseStream);

        var json = await reader.ReadToEndAsync(cancellationToken);

        var manifest = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json);

        return manifest ?? new Dictionary<string, List<string>>();
    }

    private bool IsAllowedImageKey(string key)
    {
        var ext = Path.GetExtension(key).ToLowerInvariant();
        return _allowedExtensions.Contains(ext);
    }
}