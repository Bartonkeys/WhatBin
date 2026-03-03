using BelfastBinsApi.Services;
using BelfastBinsApi.Plugins;
using BelfastBinsApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BinDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<CollectionScheduleService>();
builder.Services.AddScoped<BinScraperService>();

// Semantic Kernel setup
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"]
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? "";
var openAiModel = builder.Configuration["OpenAI:Model"] ?? "gpt-4o-mini";

builder.Services.AddScoped<BinCollectionPlugin>();

builder.Services.AddScoped<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiApiKey);

    var kernel = kernelBuilder.Build();

    // Import the plugin using the scoped instance
    var plugin = sp.GetRequiredService<BinCollectionPlugin>();
    kernel.ImportPluginFromObject(plugin, "BinCollection");

    return kernel;
});

// SMS / Twilio
builder.Services.AddSingleton<ISmsService, TwilioSmsService>();

// Night-before notification background service
builder.Services.AddHostedService<NotificationBackgroundService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
