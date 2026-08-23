using AskMyBooks.Application.Abstractions;
using AskMyBooks.Infrastructure.Llm;
using AskMyBooks.Infrastructure.Vectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Qdrant.Client;

namespace AskMyBooks.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// نقطة التبديل الوحيدة للـ "AI edge".
    /// دي المكان الوحيد في المشروع كله اللي بيعرف Qdrant.Client أو تفاصيل Ollama.
    /// عشان تبدّل أي واحد فيهم: اكتب كلاس جديد بينفذ نفس الـ interface،
    /// وغيّر السطر المناسب هنا — مفيش أي حاجة تانية في Application أو Api هتتغير.
    /// </summary>
    public static IServiceCollection AddAiEdge(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.AddHttpClient<ILlmClient, OllamaLlmClient>((sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<LlmOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl);
            http.Timeout = TimeSpan.FromMinutes(2); // الـ LLM المحلي ممكن ياخد وقت
        });

        services.Configure<QdrantOptions>(configuration.GetSection(QdrantOptions.SectionName));
        services.AddSingleton(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            return new QdrantClient(opts.Host, opts.Port);
        });
        services.AddSingleton<IVectorStore, QdrantVectorStore>();

        return services;
    }
}