using RestClient.API.Extension;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClientWithRetryPolicy2("System3", logger);
builder.Services.AddHttpClientWithRetryPolicy2("ChuckNorrisService", logger);
builder.Services.AddHttpClientWithRetryPolicy2("System2", logger);
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
