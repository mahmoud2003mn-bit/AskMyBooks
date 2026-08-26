# Ask My Books

A RAG-powered REST API for a personal **book reading journal**. You save books
you have read, are reading, or want to read, then ask questions about them in
plain language and get answers grounded in your own notes, not the model's
general knowledge of those books.

Built for the "Ask-My-Data" capstone: clean layered .NET (EF Core + Postgres)
combined with the AI edge (Ollama + Qdrant, RAG). The whole stack runs on
**Kubernetes**, started by a single script.

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
  AskMyBooks.Domain/          Encapsulated model (BookEntry) - no framework dependencies
  AskMyBooks.Application/     Interfaces (ports), DTOs, services - the business logic
  AskMyBooks.Infrastructure/  EF Core/Postgres, Ollama, Qdrant - the adapters
  AskMyBooks.Api/             Controllers, DI wiring, middleware, browser UI

k8s/
  postgres.yaml                Postgres (with persistent storage)
  qdrant.yaml                  Qdrant vector database
  pgadmin.yaml                 pgAdmin (with persistent storage)
  api.yaml                     The AskMyBooks API itself
  gateway.yaml                 nginx reverse proxy — single entry point for everything
```

Dependencies point inward: `Api -> Infrastructure -> Application -> Domain`.

`Application` never references `Infrastructure`. It only knows the
`ILlmClient` / `IVectorStore` / `IBookRepository` interfaces, so either concrete
implementation can be swapped without touching a line of business logic.

### Controllers are split by single action, one file each

Following the same principle applied throughout this project's sibling
system (VacationsSystem): each controller has exactly one responsibility.

| Controller | Action |
|---|---|
| `CreateBookController` | `POST /books` |
| `GetBooksController` | `GET /books` |
| `DeleteBookController` | `DELETE /books/{id}` |
| `ExtractController` | `POST /books/extract` (a separate AI feature, not plain CRUD) |
| `AskController` | `POST /ask` (a separate AI feature) |

DTOs (`CreateBookRequest`, `BookResponse`, etc.) live in their own files under
`Application/Dtos/`, and the interfaces (`IBookService`, `IAskService`) work
with these DTOs directly — `BookService`/`AskService` are the only things that
implement them, keeping the dependency graph one-directional.

---

## Deployment

The whole stack runs on **Kubernetes** (local `minikube` cluster), started by
a single script — no manual `docker compose` steps needed.

### One-command startup

```powershell
.\start-askmybooks-k8s.ps1
```

This script:
1. Checks Docker is running (and tells you to start it if not)
2. Checks/starts minikube
3. Builds the API image
4. Loads all required images into minikube (Postgres, Qdrant, pgAdmin, nginx)
5. Applies every manifest in `k8s/`
6. Waits for every pod to be ready
7. Applies EF Core migrations against the in-cluster Postgres automatically (via a temporary port-forward) — safe to re-run, a no-op if the schema is already up to date
8. Opens a port-forward to the gateway and prints the URLs

### Single entry point (nginx gateway)

Rather than exposing each service on its own port, an nginx reverse proxy
routes everything through one address:

| Path | Goes to |
|---|---|
| `/` and `/books`, `/ask`, `/books/extract`, `/swagger` | The API |
| `/pgadmin/` | pgAdmin (database admin UI) |
| `/qdrant/collections` | Qdrant REST API |

```powershell
kubectl port-forward service/askmybooks-gateway-service 5090:80
```

Then open `http://localhost:5090`.

### Persistent storage

Postgres and pgAdmin both use a `PersistentVolumeClaim`, so data and pgAdmin's
registered server connection survive pod restarts (a Docker/minikube restart,
`kubectl delete pod`, etc.) — earlier iterations used `emptyDir` and lost
everything on every restart, which is exactly why this was changed.

### Public access (optional)

```powershell
ngrok http 5090
```

> ⚠️ This exposes pgAdmin publicly too. Add `--basic-auth="user:pass"` if
> sharing the tunnel for more than a quick test, and close it when done.

### Networking inside the cluster

Pods reach each other by Kubernetes **Service name**, not `localhost`:

| Service | Reached at |
|---|---|
| Postgres | `postgres-service:5432` |
| Qdrant | `qdrant-service:6334` (gRPC) |
| pgAdmin | `pgadmin-service:80` |

**Ollama is not part of the cluster** — it runs directly on the host machine
so it can use a GPU if available. The API reaches it via the minikube
gateway IP (`192.168.49.1:11434` on this setup, found with
`minikube ssh -- "ip route show default"`), **not**
`host.minikube.internal`, which does not resolve reliably on the Docker
driver on Windows in this environment.

Pull the two models on the host before first use:

```powershell
ollama pull qwen2.5:3b        # chat: extraction + answering
ollama pull nomic-embed-text  # embeddings: 768 dimensions
```

---

## Endpoints

| Method | Route | Behaviour |
|--------|-------|-----------|
| `POST` | `/books` | Validate -> save to Postgres -> embed -> index in Qdrant -> **201** |
| `GET` | `/books` | List from Postgres, optional `?status=` / `?author=` -> **200** |
| `DELETE` | `/books/{id}` | Remove from Postgres **and** Qdrant -> **204** / **404** |
| `POST` | `/books/extract` | Free text -> LLM (JSON) -> validated record -> saved -> **201** / **400** |
| `POST` | `/ask` | Question -> Qdrant retrieval -> grounded answer + sources -> **200** |

`Create` responses use `Created($"/books/{id}", result)` rather than
`CreatedAtAction` — with the controllers split one-action-per-class, ASP.NET
Core's route-name lookup for `CreatedAtAction` couldn't reliably resolve the
target action across separate controller classes, throwing
`InvalidOperationException: No route matches the supplied values` even
though the underlying save succeeded. A direct URI sidesteps the route
lookup entirely.

---

## How the honest refusal works

`AskService` embeds the question and searches Qdrant with a `MinScore`
threshold. If **no book clears the bar**, it returns immediately with a fixed
message and an empty `sources` array — the LLM is never called at all. There is
no opportunity to hallucinate, because nothing was asked.

When books *do* clear the bar, the system prompt constrains the model to the
retrieved excerpts only, and instructs it to say it does not have the
information rather than guess.

---

## Swapping the LLM or vector store

Both seams are single interfaces:

- `ILlmClient` — implement against OpenAI, Azure OpenAI, Anthropic, etc.
- `IVectorStore` — implement against Pinecone, pgvector, Weaviate, etc.

Write the new class, then change the matching line in
`Infrastructure/ServiceCollectionExtensions.cs` -> `AddAiEdge()`. Nothing in
`Application` or `Api` changes, because `BookService` and `AskService` only
ever see the interfaces.

Two no-op stand-ins already exist (`NoOpLlmClient`, `NoOpVectorStore`) as
documented Null Object implementations — valid substitutes per the interface
contract, not currently wired into `AddAiEdge` by default, intended for local
development without Ollama/Qdrant running.

---

## Configuration

`QdrantOptions` (Infrastructure — connection wiring) and `AskOptions`
(Application — RAG behavior) both bind from the same `"Qdrant"` config
section, but each declares only the fields it actually owns — no duplicated,
dead configuration between them:

```json
{
  "ConnectionStrings": { "Postgres": "Host=localhost;Port=5432;..." },
  "Qdrant": {
    "Host": "localhost", "Port": 6334, "Collection": "book_entries",
    "VectorSize": 768, "TopK": 5, "MinScore": 0.42
  },
  "Llm": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "qwen2.5:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

- `Port` is Qdrant's **gRPC** port — must match what Qdrant is actually
  listening on. In Kubernetes this is `6334` regardless of any external
  port mapping; a mismatch here (an earlier version of this project used
  `7334`, which doesn't match anything Qdrant listens on by default) causes
  every embed/search call to fail outright.
- `VectorSize` must match the embedding model's output (768 for `nomic-embed-text`)
- `MinScore` is the relevance cutoff described above
- `TopK` caps how many excerpts get sent to the model

In Kubernetes, the equivalent values are set as environment variables in
`k8s/api.yaml` (`ConnectionStrings__Postgres`, `Qdrant__Host`, `Qdrant__Port`,
`Llm__BaseUrl`), which take precedence over `appsettings.json`.

---

## Demo checklist

- [ ] `.\start-askmybooks-k8s.ps1` brings up every service and applies migrations
- [ ] Add 2–4 books via `/books/extract` with free-text notes
- [ ] Ask a question worded differently from any note -> finds the right book
- [ ] Ask something out of scope -> honest refusal, empty sources
- [ ] `POST /books/extract` with gibberish -> 400, not a crash
- [ ] `DELETE /books/{id}` -> gone from `GET /books` and from the Qdrant dashboard
- [ ] Delete the same id again -> 404
- [ ] `kubectl delete pod -l app=askmybooks-postgres` -> data survives (PVC)

---

## Known constraints

- **First LLM call is slow.** Loading a model into memory can take anywhere
  from a few seconds to over a minute depending on hardware and model size.
  Subsequent calls are much faster. The HTTP client timeout is set to two
  minutes to accommodate this.
- **`emptyDir` volumes lose data on pod recreation.** Postgres and pgAdmin
  were switched to `PersistentVolumeClaim` for exactly this reason — if any
  other service in `k8s/` still uses `emptyDir` (Qdrant currently does), its
  data will not survive a pod restart.
- **Ollama is not containerized in this cluster**, so it must already be
  reachable from the cluster's gateway IP before running the deployment
  script. This is intentional: it lets Ollama use a GPU when one is
  available, independent of the API's own lifecycle, and the correct IP can
  change if minikube is deleted and recreated — re-check it with
  `minikube ssh -- "ip route show default"` if `/books/extract` or `/ask`
  start failing to reach Ollama after a cluster rebuild.