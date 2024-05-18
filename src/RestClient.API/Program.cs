using Polly;
using RestClient.API.Extension;
using RestClient.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();

builder.Services.AddLogging();
builder.Services.AddScoped<ChuckNorrisService>();
builder.Services.AddSingleton<IPipelineBuilder, PipelineBuilder>();

builder.Services.AddHttpClientWithRetryPolicy(logger);
//builder.Services.AddHttpClientWithRetryPolicy("System3", logger);
//builder.Services.AddHttpClientWithRetryPolicy("ChuckNorrisService", logger);
//builder.Services.AddHttpClientWithRetryPolicy("System2", logger);


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
