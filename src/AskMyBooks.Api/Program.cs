using AskMyBooks.Api.Middleware;
using AskMyBooks.Application.Abstractions;
using AskMyBooks.Application.Services;
using AskMyBooks.Infrastructure;
using AskMyBooks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IBookService, BookService>();

builder.Services.Configure<AskOptions>(builder.Configuration.GetSection(AskOptions.SectionName));
builder.Services.AddScoped<IAskService, AskService>();

// The "AI edge": Ollama behind ILlmClient, Qdrant behind IVectorStore.
// See AskMyBooks.Infrastructure.ServiceCollectionExtensions.AddAiEdge to swap either.
builder.Services.AddAiEdge(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Registered early so it wraps everything downstream, including the controllers.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Make sure the Qdrant collection exists before taking traffic,
// instead of failing on the first POST /books.
using (var scope = app.Services.CreateScope())
{
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    await vectorStore.EnsureCollectionAsync(CancellationToken.None);
}

app.Run();