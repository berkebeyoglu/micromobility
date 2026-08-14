using System.Text.Json.Serialization;
using MicroMobility.ParkingPhoto.Api.Configuration;
using MicroMobility.ParkingPhoto.Api.Geo;
using MicroMobility.ParkingPhoto.Api.Services;
using MicroMobility.ParkingPhoto.Api.Validation;
using MicroMobility.ParkingPhoto.Api.Validation.Rules;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => o.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "Mikromobilite Park Fotoğrafı Doğrulama API",
    Version = "v1",
    Description = "Sürüş bitişinde çekilen park fotoğrafını kaynak, konum, plaka ve duruş açısından doğrular."
}));

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.Configure<CaptureSessionOptions>(
    builder.Configuration.GetSection(CaptureSessionOptions.SectionName));
builder.Services.Configure<PhotoValidationOptions>(
    builder.Configuration.GetSection(PhotoValidationOptions.SectionName));
builder.Services.Configure<GeofenceOptions>(
    builder.Configuration.GetSection(GeofenceOptions.SectionName));
builder.Services.Configure<VisionOptions>(
    builder.Configuration.GetSection(VisionOptions.SectionName));
builder.Services.Configure<PhotoStorageOptions>(
    builder.Configuration.GetSection(PhotoStorageOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IZoneRepository>(_ => new InMemoryZoneRepository(SeedZones.Create()));
builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
builder.Services.AddSingleton<IVehicleRepository, InMemoryVehicleRepository>();
builder.Services.AddSingleton<IRideRepository, InMemoryRideRepository>();
builder.Services.AddSingleton<ICaptureSessionService, CaptureSessionService>();
builder.Services.AddSingleton<IExifReader, ExifReader>();
builder.Services.AddSingleton<IPhotoStore, FileSystemPhotoStore>();
builder.Services.AddScoped<IParkingPhotoValidator, ParkingPhotoValidator>();

builder.Services.AddScoped<IParkingPhotoRule, CameraSourceRule>();
builder.Services.AddScoped<IParkingPhotoRule, ParkingLocationRule>();
builder.Services.AddScoped<IParkingPhotoRule, UprightPostureRule>();
builder.Services.AddScoped<IParkingPhotoRule, LicensePlateRule>();

// The plate/posture model runs behind an HTTP endpoint when one is configured; otherwise the
// deterministic simulator keeps the whole flow runnable locally.
builder.Services.AddHttpClient<HttpVisionAnalyzer>((sp, client) =>
{
    var vision = sp.GetRequiredService<IOptions<VisionOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(vision.TimeoutSeconds);
});
builder.Services.AddSingleton<SimulatedVisionAnalyzer>();
builder.Services.AddScoped<IVisionAnalyzer>(sp =>
{
    var options = sp.GetRequiredService<IOptions<VisionOptions>>();
    IVisionAnalyzer inner = string.IsNullOrWhiteSpace(options.Value.Endpoint)
        ? sp.GetRequiredService<SimulatedVisionAnalyzer>()
        : sp.GetRequiredService<HttpVisionAnalyzer>();

    return new HintOverridingVisionAnalyzer(inner, options);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

/// <summary>Exposed so the integration tests can spin up the API with WebApplicationFactory.</summary>
public partial class Program;
