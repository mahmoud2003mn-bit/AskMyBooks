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

// الـ "AI edge": Ollama ورا ILlmClient، Qdrant ورا IVectorStore.
// شوف AskMyBooks.Infrastructure.ServiceCollectionExtensions.AddAiEdge
// لو عايز تبدّل أي واحد فيهم.
builder.Services.AddAiEdge(builder.Configuration);

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

// نتأكد إن الـ collection موجودة قبل ما نستقبل أي request،
// بدل ما أول POST /books يفشل.
using (var scope = app.Services.CreateScope())
{
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    await vectorStore.EnsureCollectionAsync(CancellationToken.None);
}

app.Run();