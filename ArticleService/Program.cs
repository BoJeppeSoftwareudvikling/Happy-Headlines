using ArticleService.Data;
using ArticleService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ArticleDbContextFactory>();

var app = builder.Build();

var factory = app.Services.GetRequiredService<ArticleDbContextFactory>();
var logger = app.Logger;

foreach (var continent in Enum.GetValues<Continent>())
{
    var created = false;
    for (var attempt = 1; attempt <= 20 && !created; attempt++)
    {
        try
        {
            using var db = factory.Create(continent);
            db.Database.EnsureCreated();
            created = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "EnsureCreated failed for {Continent} (attempt {Attempt}/20)", continent, attempt);
            Thread.Sleep(TimeSpan.FromSeconds(3));
        }
    }

    if (!created)
        throw new InvalidOperationException($"Could not create database for {continent}.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
