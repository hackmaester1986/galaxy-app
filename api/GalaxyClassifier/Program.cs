
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SageMakerRuntime;
using GalaxyClassifierApi.Models.Options;
using GalaxyClassifierApi.Services;
using GalaxyClassifierApi.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<GalaxyS3Options>(
    builder.Configuration.GetSection("GalaxyS3"));


var regionName = builder.Configuration["Aws:Region"] ?? "us-east-1";
var region = RegionEndpoint.GetBySystemName(regionName);

if (builder.Environment.IsDevelopment())
{
    var accessKey = builder.Configuration["AWS_ACCESS_KEY_ID"];
    var secretKey = builder.Configuration["AWS_SECRET_ACCESS_KEY"];

    if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
    {
        throw new InvalidOperationException("Missing AWS development credentials.");
    }

    var credentials = new BasicAWSCredentials(accessKey, secretKey);

    builder.Services.AddSingleton<IAmazonS3>(_ =>
        new AmazonS3Client(credentials, region));

    builder.Services.AddSingleton<IAmazonSageMakerRuntime>(_ =>
        new AmazonSageMakerRuntimeClient(credentials, region));
}
else
{
    builder.Services.AddSingleton<IAmazonS3>(_ =>
        new AmazonS3Client(region));

    builder.Services.AddSingleton<IAmazonSageMakerRuntime>(_ =>
        new AmazonSageMakerRuntimeClient(region));
}

builder.Services.AddScoped<IS3ImageService, S3ImageService>();
builder.Services.AddScoped<ISageMakerPredictionService, SageMakerPredictionService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

app.UseCors("Frontend");
app.UseAuthorization();

// Serve Angular files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// For Angular client-side routing
app.MapFallbackToFile("index.html");
app.Run();

