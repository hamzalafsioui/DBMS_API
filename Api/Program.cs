using Api;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddSingleton<DbClientService>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

app.MapPost("/query", async ([FromBody] QueryRequest request, [FromServices] DbClientService dbService) =>
{
    if (string.IsNullOrWhiteSpace(request.Query))
    {
        return Results.BadRequest(new { error = "Query cannot be empty" });
    }

    var result = await dbService.ExecuteQueryAsync(request.DbName ?? "testdb", request.Query);
    return Results.Ok(result);
});

app.Run();

public record QueryRequest(string? DbName, string Query);
