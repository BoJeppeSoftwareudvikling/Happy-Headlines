using ArticleService.Data;
using ArticleService.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ArticleDbContextFactory>();

var app = builder.Build();

var factory = app.Services.GetRequiredService<ArticleDbContextFactory>();
foreach (var continent in Enum.GetValues<Continent>())
{
    using var db = factory.Create(continent);
    db.Database.EnsureCreated();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
