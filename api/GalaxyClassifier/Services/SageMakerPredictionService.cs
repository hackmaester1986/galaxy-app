using System.Text;
using System.Text.Json;
using Amazon.SageMakerRuntime;
using Amazon.SageMakerRuntime.Model;
using GalaxyClassifierApi.Models.Dtos;
using GalaxyClassifierApi.Services.Interfaces;

namespace GalaxyClassifierApi.Services;

public sealed class SageMakerPredictionService : ISageMakerPredictionService
{
    private readonly IAmazonSageMakerRuntime _runtime;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SageMakerPredictionService> _logger;

    public SageMakerPredictionService(
        IAmazonSageMakerRuntime runtime,
        IConfiguration configuration,
        ILogger<SageMakerPredictionService> logger)
    {
        _runtime = runtime;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<PredictResponseDto> PredictFromS3UriAsync(
        string s3Uri,
        int topK = 3,
        CancellationToken cancellationToken = default)
    {
        var endpointName = _configuration["SageMaker:EndpointName"];
        var infComponentName = _configuration["SageMaker:InfComponentName"];
        if (string.IsNullOrWhiteSpace(endpointName))
            throw new InvalidOperationException("SageMaker endpoint name is missing.");

        var payload = JsonSerializer.Serialize(new
        {
            s3_uri = s3Uri,
            top_k = topK
        });

        using var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        var request = new InvokeEndpointRequest
        {
            EndpointName = endpointName,
            InferenceComponentName = infComponentName,
            ContentType = "application/json",
            Accept = "application/json",
            Body = bodyStream
        };

        var response = await _runtime.InvokeEndpointAsync(request, cancellationToken);

        using var reader = new StreamReader(response.Body);
        var responseJson = await reader.ReadToEndAsync(cancellationToken);

        var result = JsonSerializer.Deserialize<PredictResponseDto>(
            responseJson,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (result == null)
            throw new InvalidOperationException("SageMaker returned an invalid response.");

        return result;
    }
}