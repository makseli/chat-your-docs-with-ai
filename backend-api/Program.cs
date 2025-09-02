using StackExchange.Redis;
using backend_api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// CORS konfigürasyonu
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Redis konfigürasyonu - Resilient connection service
builder.Services.AddSingleton<IRedisService, RedisService>();

// File storage konfigürasyonu - Default olarak FileStorageService
var storageType = builder.Configuration["STORAGE_TYPE"] ?? "FileStorage";
if (storageType.Equals("CloudStorage", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IFileStorageService, CloudStorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, FileStorageService>();
}

// CORS konfigürasyonu
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// HTTP Clients
builder.Services.AddHttpClient<IOllamaService, OllamaService>();

// RAG Services
builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// CORS middleware
app.UseCors("AllowAll");

app.MapControllers();

// Uygulama başlangıcında log kaydet
var redisService = app.Services.GetRequiredService<IRedisService>();
_ = Task.Run(async () =>
{
    await Task.Delay(2000); // Redis bağlantısının kurulmasını bekle
    await redisService.LogEventAsync("INFO", "APP_STARTED", "Uygulama başlatıldı", details: $"Environment: {app.Environment.EnvironmentName}");
});

app.Run();