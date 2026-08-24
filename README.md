
Dependencies point inward: `Api -> Infrastructure -> Application -> Domain`.

`Application` never references `Infrastructure`. It only knows the
`ILlmClient` / `IVectorStore` / `IBookRepository` interfaces, so either concrete
implementation can be swapped without touching a line of business logic.

The whole stack (API, Postgres, Qdrant) is containerized. Ollama runs outside
the compose stack so it can use a GPU if one is available.

---

## Prerequisites

- [Docker](https://www.docker.com/) — runs Postgres, Qdrant, and the API itself
- [Ollama](https://ollama.com/) — runs the models. Install natively, or run it
  as its own container; either way it must be reachable at `localhost:11434`
  on the host machine
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) — only needed
  to run the EF Core migration once (the API itself runs inside Docker)

Pull the two models before first run:

```bash
ollama pull qwen2.5:3b        # chat: extraction + answering
ollama pull nomic-embed-text  # embeddings: 768 dimensions
```

> A smaller chat model (`qwen2.5:3b` instead of `7b`) is used by default for
> faster inference on limited hardware. Swap `Llm:ChatModel` in
> `appsettings.json` (or the `Llm__ChatModel` environment variable in
> `docker-compose.yml`) for a larger model if you have the VRAM/CPU for it.

---

## How to run

```bash
# 1. Build and start Postgres, Qdrant, and the API together
docker compose up -d

# 2. Apply EF Core migrations (first run only)
cd src/AskMyBooks.Api
dotnet ef database update --project ../AskMyBooks.Infrastructure --startup-project .
```

That is it. Every subsequent run is just:

```bash
docker compose up -d
```

Then open:

| | |
|---|---|
| **Browser UI** | http://localhost:5080 |
| Swagger | http://localhost:5080/swagger |
| Qdrant dashboard | http://localhost:7333/dashboard |

The Qdrant collection is created automatically on startup via
`IVectorStore.EnsureCollectionAsync` — no manual setup.

### Container-to-container networking

Inside Docker, services reach each other by service name rather than
`localhost`. This is set via environment variables in `docker-compose.yml`
and overrides `appsettings.json` at runtime:

| Service | Inside Docker | On the host (`dotnet run` without Docker) |
|---|---|---|
| Postgres | `Host=postgres` | `Host=localhost` |
| Qdrant | `qdrant:6334` | `localhost:7334` |
| Ollama | `host.docker.internal:11434` | `localhost:11434` |

`host.docker.internal` is how a container reaches services running directly
on the host machine (or in an unrelated container), which is how the API
reaches Ollama without Ollama being part of this compose stack.

> **Port note:** Qdrant is mapped to **7333/7334** rather than the default
> 6333/6334, to avoid colliding with another Qdrant instance that might
> already be running locally.

### Trying it out

Three ways, easiest first:

1. **Browser UI** at http://localhost:5080 — four tabs: Ask, Shelf, Add a book, From notes
2. **`requests.http`** in the repo root — open in VS Code with the
   [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
   and run top to bottom. Covers all five endpoints plus every error case.
3. **Swagger** at `/swagger`

### Sharing a running instance

To expose a locally running instance for a demo, tunnel the API port with
[ngrok](https://ngrok.com/):

```bash
ngrok http 5080
```

This gives a public HTTPS URL that forwards to the browser UI, Swagger, and
every endpoint. Postgres, Qdrant, and Ollama stay local; only the API port is
exposed.

---

## Endpoints

| Method | Route | Behaviour |
|--------|-------|-----------|
| `POST` | `/books` | Validate -> save to Postgres -> embed -> index in Qdrant -> **201** |
| `GET` | `/books` | List from Postgres, optional `?status=` / `?author=` -> **200** |
| `DELETE` | `/books/{id}` | Remove from Postgres **and** Qdrant -> **204** / **404** |
| `POST` | `/books/extract` | Free text -> LLM (JSON) -> validated record -> saved -> **201** / **400** |
| `POST` | `/ask` | Question -> Qdrant retrieval -> grounded answer + sources -> **200** |

---

## Where each course topic lives

| Topic | Where |
|-------|-------|
| Encapsulated domain model | `Domain/BookEntry.cs` — private setters, invariants enforced in the `Create()` factory (rating and finish date only valid when status is `Finished`) |
| DTOs at the HTTP boundary | `Application/Dtos/` — `CreateBookRequest`, `BookResponse`, `ExtractRequest`, `AskRequest`/`AskResponse`. The domain entity never leaves the service layer |
| DbContext and EF mapping | `Infrastructure/Persistence/AppDbContext.cs`, `Configurations/BookEntryConfiguration.cs`, `Migrations/` |
| Repository, no raw SQL | `Infrastructure/Persistence/BookRepository.cs` — LINQ over `DbSet<BookEntry>` |
| Service layer | `Application/Services/BookService.cs`, `AskService.cs` — all business logic lives here |
| `ILlmClient` interface | `Application/Abstractions/ILlmClient.cs` |
| Ollama implementation | `Infrastructure/Llm/OllamaLlmClient.cs` |
| `IVectorStore` interface | `Application/Abstractions/IVectorStore.cs` |
| Qdrant implementation | `Infrastructure/Vectors/QdrantVectorStore.cs` |
| One-registration provider swap | `Infrastructure/ServiceCollectionExtensions.cs` -> `AddAiEdge()` — the only place in the solution that references `Qdrant.Client` or Ollama''s URL |
| Config-driven, no secrets | `appsettings.json`: `ConnectionStrings`, `Qdrant`, `Llm` sections, overridable via `docker-compose.yml` environment variables or an untracked `appsettings.Development.json` |
| Bad LLM response -> 400 | `OllamaLlmClient.CompleteJsonAsync` validates JSON syntax; `BookService.ExtractAsync` validates the schema and required fields; both throw `LlmContractException`, which the middleware maps to 400 |
| Global exception handling | `Api/Middleware/ExceptionHandlingMiddleware.cs` — `NotFoundException`->404, `ArgumentException`/`LlmContractException`->400, everything else->500 and logged |
| RAG retrieval and grounding | `Application/Services/AskService.cs` — embeds the question, searches Qdrant above a similarity threshold, returns an honest refusal when nothing clears it, otherwise prompts the model to answer only from the retrieved excerpts |
| Controllers as a pure boundary | `Api/Controllers/BooksController.cs`, `AskController.cs` — no try/catch, no validation, no logic. Just delegation and status codes |
| Compensating transaction | `BookService.CreateAsync` — if the Qdrant upsert fails after the Postgres insert, the row is rolled back so the two stores never drift |
| Containerization | `Dockerfile` (multi-stage build), `docker-compose.yml` (three services: `postgres`, `qdrant`, `api`) |

---

## How the honest refusal works

This is the part worth understanding, because it is what separates RAG from a
chatbot guessing.

`AskService` embeds the question and searches Qdrant with a `MinScore`
threshold. If **no book clears the bar**, it returns immediately with a fixed
message and an empty `sources` array — the LLM is never called at all. There is
no opportunity to hallucinate, because nothing was asked.

When books *do* clear the bar, the system prompt constrains the model to the
retrieved excerpts only, and instructs it to say it does not have the
information rather than guess.

Real scores from a populated shelf:

| Question | Top score | Result |
|---|---|---|
| "surviving alone in a hostile place" | 0.4927 | clears 0.42 -> answered from *The Martian* |
| "pizza toppings" | 0.3720 | below 0.42 -> refused, nothing retrieved |

The browser UI draws this threshold as a dashed red line across the score
chart, so you can see exactly why a question was answered or refused.

---

## Swapping the LLM or vector store

Both seams are single interfaces:

- `ILlmClient` — implement against OpenAI, Azure OpenAI, Anthropic, etc.
- `IVectorStore` — implement against Pinecone, pgvector, Weaviate, etc.

Write the new class, then change the matching line in
`Infrastructure/ServiceCollectionExtensions.cs` -> `AddAiEdge()`. Nothing in
`Application` or `Api` changes, because `BookService` and `AskService` only
ever see the interfaces.

Two no-op stand-ins already exist (`NoOpLlmClient`, `NoOpVectorStore`) for
running CRUD without Ollama or Qdrant available.

---

## Configuration

```json
{
  "ConnectionStrings": { "Postgres": "Host=localhost;Port=5432;..." },
  "Qdrant": {
    "Host": "localhost", "Port": 7334, "Collection": "book_entries",
    "VectorSize": 768, "TopK": 5, "MinScore": 0.42
  },
  "Llm": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "qwen2.5:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

When running via `docker compose`, the equivalent values are set as
environment variables in `docker-compose.yml` (`ConnectionStrings__Postgres`,
`Qdrant__Host`, `Qdrant__Port`, `Llm__BaseUrl`), which take precedence over
`appsettings.json`. Update both places if you change a value, so the local
`dotnet run` path and the containerized path stay in sync.

- `VectorSize` must match the embedding model''s output (768 for `nomic-embed-text`)
- `MinScore` is the relevance cutoff described above
- `TopK` caps how many excerpts get sent to the model

The UI mirrors `MinScore` in a constant at the top of `wwwroot/index.html` —
update both if you change it.

---

## Helper scripts

Two PowerShell scripts for inspecting retrieval without going through the API:

```powershell
.\search.ps1 "surviving alone in a hostile place"   # scores for a phrase
.\similar.ps1 "<book-id>"                           # books nearest an existing book
```

Both print a score table. Useful for tuning `MinScore`, since they skip the
slow LLM call entirely. Point them at Qdrant on `localhost:7333` regardless of
whether the API itself is running in Docker or via `dotnet run`.

---

## Demo checklist

- [ ] `docker compose up -d` brings up all three services
- [ ] Add 2 to 3 books with distinct notes
- [ ] Ask a question worded differently from any note -> finds the right book
- [ ] Ask something out of scope -> honest refusal, empty sources
- [ ] `POST /books/extract` with a messy note -> clean structured record
- [ ] `POST /books/extract` with gibberish -> 400, not a crash
- [ ] `DELETE /books/{id}` -> gone from `GET /books` and from the Qdrant dashboard
- [ ] Delete the same id again -> 404

---

## Known constraints

- **First LLM call is slow.** Loading a model into memory can take anywhere
  from a few seconds to over a minute depending on hardware and model size.
  Subsequent calls are much faster. The HTTP client timeout is set to two
  minutes to accommodate this.
- **Scores cluster in a narrow band.** `ToEmbeddingText()` includes fixed
  scaffolding (`Genre:`, `Status:`, `Rating:`) in every embedded string, which
  makes all vectors somewhat similar. The 0.42 threshold was chosen
  empirically against real queries rather than picked as a round number.
- **Ollama is not containerized in this compose stack**, so it must already be
  reachable at `localhost:11434` before `docker compose up -d`. This is
  intentional: it lets Ollama use a GPU when one is available, independent of
  the API''s own lifecycle.
'@ | Set-Content -Path "D:\AskMyBooks\README.md" -Encoding UTF8