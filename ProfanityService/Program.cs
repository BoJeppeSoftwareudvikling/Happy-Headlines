using ProfanityService.Data;
using ProfanityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ProfanityDbContextFactory>();
builder.Services.Configure<DatabaseInitializationOptions>(
    builder.Configuration.GetSection(DatabaseInitializationOptions.SectionName));
builder.Services.AddSingleton<ProfanityDatabaseInitializer>();

var app = builder.Build();

var initializer = app.Services.GetRequiredService<ProfanityDatabaseInitializer>();
await initializer.InitializeAsync(app.Lifetime.ApplicationStopping);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
