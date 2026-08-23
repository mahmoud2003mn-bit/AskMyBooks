# Ask My Books

A RAG-powered REST API for a personal **book reading journal**. You save books
you've read, are reading, or want to read — then ask questions about them in
plain language and get answers grounded in your own notes, not the model's
general knowledge of those books.

Built for the "Ask-My-Data" capstone: clean layered .NET (EF Core + Postgres)
combined with the AI edge (Ollama + Qdrant, RAG).

---

## What it does

| | |
|---|---|
| **Semantic search** | "Which book was about someone stranded in space?" finds *Project Hail Mary* — no shared keywords with the note |
| **Grounded answers** | Answers are composed only from retrieved excerpts; sources and similarity scores are returned with every answer |
| **Honest refusal** | An out-of-scope question returns "I don't have anything in your books that answers that" instead of inventing one |
| **Assisted entry** | Paste a messy note, the LLM extracts a structured record and files it |
| **Two stores, one truth** | Postgres is the system of record; Qdrant is the vector index. Deletes remove from both |

---

## Architecture

```
src/
  AskMyBooks.Domain/          Encapsulated model (BookEntry) — no framework dependencies
  AskMyBooks.Application/     Interfaces (ports), DTOs, services — the business logic
  AskMyBooks.Infrastructure/  EF Core/Postgres, Ollama, Qdrant — the adapters
  AskMyBooks.Api/             Controllers, DI wiring, middleware, browser UI
```

Dependencies point inward: `Api → Infrastructure → Application → Domain`.

`Application` never references `Infrastructure`. It only knows the
`ILlmClient` / `IVectorStore` / `IBookRepository` interfaces, so either concrete
implementation can be swapped without touching a line of business logic.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/) — runs Postgres and Qdrant
- [Ollama](https://ollama.com/) — runs the models

Pull the two models before first run:

```bash
ollama pull qwen2.5:7b        # chat: extraction + answering
ollama pull nomic-embed-text  # embeddings: 768 dimensions
```

---

## How to run

```bash
# 1. Start Postgres + Qdrant
docker compose up -d

# 2. Apply EF Core migrations
cd src/AskMyBooks.Api
dotnet ef database update --project ../AskMyBooks.Infrastructure --startup-project .

# 3. Run the API
dotnet run
```

Then open:

| | |
|---|---|
| **Browser UI** | http://localhost:5226 |
| Swagger | http://localhost:5226/swagger |
| Qdrant dashboard | http://localhost:7333/dashboard |

The Qdrant collection is created automatically on startup via
`IVectorStore.EnsureCollectionAsync` — no manual setup.

> **Port note:** Qdrant is mapped to **7333/7334** rather than the default
> 6333/6334, to avoid colliding with another Qdrant instance already running
> locally. `appsettings.json` points at 7334 to match.

### Trying it out

Three ways, easiest first:

1. **Browser UI** at http://localhost:5226 — four tabs: Ask, Shelf, Add a book, From notes
2. **`requests.http`** in the repo root — open in VS Code with the
   [REST Client extension](https://marketplace.visualstudio.com/items?itemName=humao.rest-client)
   and run top to bottom. Covers all five endpoints plus every error case.
3. **Swagger** at `/swagger`

---

## Endpoints

| Method | Route | Behaviour |
|--------|-------|-----------|
| `POST` | `/books` | Validate → save to Postgres → embed → index in Qdrant → **201** |
| `GET` | `/books` | List from Postgres, optional `?status=` / `?author=` → **200** |
| `DELETE` | `/books/{id}` | Remove from Postgres **and** Qdrant → **204** / **404** |
| `POST` | `/books/extract` | Free text → LLM (JSON) → validated record → saved → **201** / **400** |
| `POST` | `/ask` | Question → Qdrant retrieval → grounded answer + sources → **200** |

---

## Where each course topic lives

| Topic | Where |
|-------|-------|
| Encapsulated domain model | `Domain/BookEntry.cs` — private setters, invariants enforced in the `Create()` factory (rating and finish date only valid when status is `Finished`) |
| DTOs at the HTTP boundary | `Application/Dtos/` — `CreateBookRequest`, `BookResponse`, `ExtractRequest`, `AskRequest`/`AskResponse`. The domain entity never leaves the service layer |
| DbContext & EF mapping | `Infrastructure/Persistence/AppDbContext.cs`, `Configurations/BookEntryConfiguration.cs`, `Migrations/` |
| Repository, no raw SQL | `Infrastructure/Persistence/BookRepository.cs` — LINQ over `DbSet<BookEntry>` |
| Service layer | `Application/Services/BookService.cs`, `AskService.cs` — all business logic lives here |
| `ILlmClient` interface | `Application/Abstractions/ILlmClient.cs` |
| Ollama implementation | `Infrastructure/Llm/OllamaLlmClient.cs` |
| `IVectorStore` interface | `Application/Abstractions/IVectorStore.cs` |
| Qdrant implementation | `Infrastructure/Vectors/QdrantVectorStore.cs` |
| One-registration provider swap | `Infrastructure/ServiceCollectionExtensions.cs` → `AddAiEdge()` — the only place in the solution that references `Qdrant.Client` or Ollama's URL |
| Config-driven, no secrets | `appsettings.json`: `ConnectionStrings`, `Qdrant`, `Llm` sections. Override via environment variables or an untracked `appsettings.Development.json` |
| Bad LLM response → 400 | `OllamaLlmClient.CompleteJsonAsync` validates JSON syntax; `BookService.ExtractAsync` validates the schema and required fields; both throw `LlmContractException`, which the middleware maps to 400 |
| Global exception handling | `Api/Middleware/ExceptionHandlingMiddleware.cs` — `NotFoundException`→404, `ArgumentException`/`LlmContractException`→400, everything else→500 and logged |
| RAG retrieval + grounding | `Application/Services/AskService.cs` — embeds the question, searches Qdrant above a similarity threshold, returns an honest refusal when nothing clears it, otherwise prompts the model to answer only from the retrieved excerpts |
| Controllers as a pure boundary | `Api/Controllers/BooksController.cs`, `AskController.cs` — no try/catch, no validation, no logic. Just delegation and status codes |
| Compensating transaction | `BookService.CreateAsync` — if the Qdrant upsert fails after the Postgres insert, the row is rolled back so the two stores never drift |

---

## How the honest refusal works

This is the part worth understanding, because it's what separates RAG from a
chatbot guessing.

`AskService` embeds the question and searches Qdrant with a `MinScore`
threshold. If **no book clears the bar**, it returns immediately with a fixed
message and an empty `sources` array — the LLM is never called at all. There is
no opportunity to hallucinate, because nothing was asked.

When books *do* clear the bar, the system prompt constrains the model to the
retrieved excerpts only, and instructs it to say it doesn't have the
information rather than guess.

Real scores from a populated shelf:

| Question | Top score | Result |
|---|---|---|
| "surviving alone in a hostile place" | 0.4927 | clears 0.42 → answered from *The Martian* |
| "pizza toppings" | 0.3720 | below 0.42 → refused, nothing retrieved |

The browser UI draws this threshold as a dashed red line across the score
chart, so you can see exactly why a question was answered or refused.

---

## Swapping the LLM or vector store

Both seams are single interfaces:

- `ILlmClient` — implement against OpenAI, Azure OpenAI, Anthropic, etc.
- `IVectorStore` — implement against Pinecone, pgvector, Weaviate, etc.

Write the new class, then change the matching line in
`Infrastructure/ServiceCollectionExtensions.cs` → `AddAiEdge()`. Nothing in
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
    "ChatModel": "qwen2.5:7b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

- `VectorSize` must match the embedding model's output (768 for `nomic-embed-text`)
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
slow LLM call entirely.

---

## Demo checklist

- [ ] Add 2–3 books with distinct notes
- [ ] Ask a question worded differently from any note → finds the right book
- [ ] Ask something out of scope → honest refusal, empty sources
- [ ] `POST /books/extract` with a messy note → clean structured record
- [ ] `POST /books/extract` with gibberish → 400, not a crash
- [ ] `DELETE /books/{id}` → gone from `GET /books` and from the Qdrant dashboard
- [ ] Delete the same id again → 404

---

## Known constraints

- **First LLM call is slow.** Loading `qwen2.5:7b` into memory can take 30–80
  seconds. Subsequent calls are much faster. The HTTP client timeout is set to
  two minutes to accommodate this.
- **Scores cluster in a narrow band.** `ToEmbeddingText()` includes fixed
  scaffolding (`Genre:`, `Status:`, `Rating:`) in every embedded string, which
  makes all vectors somewhat similar. The 0.42 threshold was chosen empirically
  against real queries rather than picked as a round number.