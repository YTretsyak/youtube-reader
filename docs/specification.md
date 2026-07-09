# youtube-reader — Specification

Buildable specification distilled from [`CLAUDE.md`](../CLAUDE.md).
`CLAUDE.md` remains the authoritative source for conventions and workflow;
this document turns its intent into enumerated requirements, a proposed API
and data model, and an ordered build path. Where this file adds detail beyond
CLAUDE.md (API contract, document shape), it is marked **proposed** — a design
proposal for confirmation, not settled fact.

---

## 1. Overview

youtube-reader takes a YouTube video URL, fetches its transcript, and produces
a short LLM-generated summary. Processed videos and their summaries are
persisted so a user can browse a history of previously summarized links instead
of re-processing them.

UX and feature-scope reference: <https://notegpt.io/youtube-video-summarizer>

**Core flow:**

```
user pastes YouTube link
  → api creates a record, publishes a transcript request to RabbitMQ, returns 202
  → transcript-service (YoutubeExplode) consumes it, publishes the transcript back
  → api consumes the transcript, sends it to the LLM for summarization
  → summary persisted; client polls until it appears, then renders it
  → link appears in browsable history
```

---

## 2. Functional requirements

| ID  | Requirement |
| --- | --- |
| FR1 | Accept a YouTube URL and validate/parse it. Parsing and validation are owned by a `VideoId`/`VideoUrl` value object in `Domain`. |
| FR2 | Fetch the transcript for the identified video. |
| FR3 | Summarize the transcript via an LLM. |
| FR4 | Persist the processed video (link + processing result) as one MongoDB document. |
| FR5 | When a link already exists with status `processed`, return the stored summary instead of re-processing it. |
| FR6 | List/browse the history of previously summarized videos. |
| FR7 | Validate every incoming request and reject malformed/oversized input before any processing (see section 3). |
| FR8 | Track each video's processing state via a `status` field (`new` → `fetching-transcript` → `summarizing` → `processed`\|`failed`). A repeat request for a URL that is still in-flight (any status except `processed`/`failed`) must not start a duplicate — it returns the in-flight record's current status. |
| FR9 | Protect the API against abuse / DDoS via rate limiting and request-size limits (see section 3). |

---

## 3. Non-functional requirements & constraints

- **Architecture**: Follow SOLID, Clean Architecture, and DDD.
- **Swap boundary**: The `ISummarizer` (LLM provider) and `ITranscriptFetcher`
  (transcript source) boundaries are strict. Nothing above `Infrastructure`
  knows which concrete provider is in use — both are expected to be swapped out.
- **Thin frontend**: The React client is a thin client over the backend HTTP
  API. No business logic (summarization rules, transcript parsing) is
  duplicated on the frontend. **Material UI (MUI)** is used for presentation
  only — components and theming, never business rules.
- **Comments**: No comments explaining *what* code does — only *why*, when
  non-obvious.
- **No speculation**: Don't add abstractions or config beyond what the current
  use case needs.

### Request validation

- Every request is validated at the `Api` boundary before any use case runs.
  Reject with `400` on: missing/empty `url`, a `url` that isn't a well-formed
  YouTube link (rejected by the `VideoUrl` value object — the single source of
  truth for URL validity), wrong content-type, or a body over the size limit.
- Domain invariants (a valid `VideoId`, non-empty transcript) are enforced in
  `Domain`/`Application`, not re-implemented on the frontend.

### Abuse / DDoS protection

- Apply ASP.NET Core **rate limiting** (`AddRateLimiter`) — per-IP (and later
  per-user) request caps; over-limit requests get `429 Too Many Requests`.
- Enforce a **max request body size** and request timeouts so a single caller
  can't tie up resources.
- The `status`-based dedupe (FR8) is itself a protection: repeated submissions
  of the same URL collapse onto one in-flight record instead of fanning out
  into many concurrent LLM calls.
- **Note on scope**: app-level rate limiting stops abusive callers, but true
  volumetric DDoS is an **edge/network** concern (CDN / WAF / reverse proxy
  such as Cloudflare or Azure Front Door). That layer sits in front of the
  `api` container and is out of scope for the app code itself.

---

## 4. Architecture & solution layout

Four backend layers, dependencies pointing inward. **Dependency rule:** nothing
above `Infrastructure` may reference YoutubeExplode, an LLM SDK, or MongoDB —
only abstractions.

| Layer | Responsibility | Key types |
| --- | --- | --- |
| `Domain` | Core model. No framework/infrastructure references. | `Video`, `Transcript`, `Summary`, `VideoId`/`VideoUrl` (owns URL parsing/validation) |
| `Application` | Use cases, expressed against interfaces only. No YoutubeExplode / LLM SDK / DB / broker SDK references. | `SummarizeVideo` (submit), transcript-result handler, `GetSummaryHistory`; `ITranscriptRequestPublisher`, `ISummarizer`, `ISummaryRepository` |
| `Infrastructure` | Concrete adapters for the abstractions. | `RabbitMqTranscriptRequestPublisher : ITranscriptRequestPublisher`, `<Provider>Summarizer : ISummarizer`, `MongoSummaryRepository : ISummaryRepository`, RabbitMQ consumer plumbing |
| `Api` | Minimal API endpoints + DI composition root + transcript-result consumer. Thin: maps HTTP to use-case calls, no business logic. | endpoint definitions, DI wiring, result-consumer host |
| `TranscriptService` | Standalone worker (separate deployable). Consumes transcript requests, runs YoutubeExplode, publishes results. Independently scalable. | `YoutubeExplodeTranscriptFetcher : ITranscriptFetcher`, request consumer |
| `Contracts` | Integration-event schemas shared by `api` + `TranscriptService`. No logic. | `TranscriptRequested`, `TranscriptReady`, `TranscriptUnavailable` |

Proposed folder tree:

```
src/
  Domain/            Video, Transcript, Summary, VideoId/VideoUrl
  Contracts/         integration-event schemas shared by api + transcript-service
                     (TranscriptRequested, TranscriptReady, TranscriptUnavailable)
  Application/       SummarizeVideo (submit), transcript-result handler,
                     GetSummaryHistory; ITranscriptRequestPublisher,
                     ISummarizer, ISummaryRepository
  Infrastructure/    RabbitMqTranscriptRequestPublisher, <Provider>Summarizer,
                     MongoSummaryRepository, RabbitMQ consumer plumbing
  Api/               minimal API endpoints + DI composition root +
                     transcript-result consumer (Dockerfile lives here)
  TranscriptService/ worker: consumes TranscriptRequested, runs
                     YoutubeExplodeTranscriptFetcher (ITranscriptFetcher),
                     publishes TranscriptReady/TranscriptUnavailable
                     (Dockerfile lives here)
web/                 React (TypeScript) client, Material UI (MUI) (Dockerfile lives here)
tests/
  Domain.Tests/           unit tests
  Application.Tests/       unit tests
  Infrastructure.Tests/    integration tests for adapters
  TranscriptService.Tests/ integration tests for the transcript worker
docker-compose.yml    mongo + rabbitmq + transcript-service + api + web
.env.example          template for the variables compose injects
```

### Transcript service — YoutubeExplode over RabbitMQ

Transcript fetching runs in its **own service** (`TranscriptService`), separate
from `api`, so the fragile, rate-limited part can be **scaled independently**
(multiple replicas as competing consumers on one queue) and isolated from the
rest of the app. `api` and the transcript service communicate **asynchronously
via RabbitMQ** — never by direct HTTP. RabbitMQ is hosted as its own container
(section 9), alongside `mongo`.

Flow:

1. `api` creates the record (`new`) and publishes a `TranscriptRequested`
   message (carrying `videoId`/`videoUrl`), then returns `202`.
2. `TranscriptService` consumes it and runs YoutubeExplode — an in-process .NET
   library used **only here**, via `YoutubeExplodeTranscriptFetcher :
   ITranscriptFetcher`. Two calls: metadata (`title`) and closed captions:

   ```csharp
   var video    = await youtube.Videos.GetAsync(videoUrl);            // Title, Author, Duration
   var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoUrl);
   var trackInfo = manifest.GetByLanguage("en");                      // choose a caption track
   var track    = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo);
   var transcript = string.Join(" ", track.Captions.Select(c => c.Text));
   ```

   Each caption exposes `.Text`, `.Offset`, `.Duration`; only `.Text` joined
   into one string is needed for summarization.
3. It publishes the outcome back: `TranscriptReady` (`videoId`, `title`,
   `transcript`) or `TranscriptUnavailable` (`videoId`, `reason`).
4. `api` consumes that result, runs the LLM (`ISummarizer`), and updates the
   record to `processed` or `failed`.

Responsibilities and caveats:

- **No captions is a normal outcome, not a crash.** An empty manifest / no
  matching language means "transcript unavailable" → published as
  `TranscriptUnavailable`, record status `failed` (FR2, section 6). It is a
  normal result message, not an exception that dead-letters.
- **Reuse one `HttpClient`** via `IHttpClientFactory` inside the service rather
  than `new YoutubeClient()` per message.
- **It scrapes reverse-engineered YouTube endpoints** — inherently fragile and
  can be IP-throttled under load (fetches originate from the transcript
  service's IP; scaling replicas spreads that load). The FR8 status-dedupe still
  limits work to one request per URL. Keep the package updated; when it breaks,
  the fix is contained to this one service — Application/Domain/Api don't change.
- **Messaging reliability**: message handling is **at-least-once with manual
  ack** — see "Message delivery & acknowledgment" just below.
- The `ITranscriptFetcher` boundary is unchanged — it now lives **inside** the
  transcript service. `api` depends only on `ITranscriptRequestPublisher`
  (Application), implemented in `Infrastructure` over RabbitMQ, and never
  references YoutubeExplode.

### Message delivery & acknowledgment

Both consumers (`transcript-service` on requests, `api` on results) use
**manual acknowledgment / at-least-once delivery**. The rules:

1. **Manual ack, ack last.** Auto-ack is off. The transcript worker acks a
   `TranscriptRequested` only *after* it has fetched and **published the result**
   (`TranscriptReady`/`TranscriptUnavailable`): `consume → fetch → publish → ack`.
   Never ack before publishing — a crash in that window loses the work with the
   message already gone.
2. **Crash mid-processing → automatic redelivery.** If the worker dies before
   acking, the broker returns the unacked delivery to the queue and redelivers it
   (to another replica or the same one on restart). No message is lost; the DB
   record simply stays in `fetching-transcript` until the redelivery completes.
   This is the scenario manual ack exists for.
3. **At-least-once ⇒ idempotent handlers.** A message can be delivered more than
   once — e.g. a crash *after* publishing the result but *before* the ack
   republishes it. So every consumer is idempotent: re-fetching is read-only and
   safe; the `api` result consumer treats a result for a video already
   `processed`/`failed` as a **no-op** (section 6). The unique `videoId` record +
   status check make duplicates harmless.
4. **"No captions" acks — it is success, not failure.** Publishing
   `TranscriptUnavailable` is a *handled* outcome: the worker acks. It must
   **not** nack/requeue, or a legitimately caption-less video loops forever.
5. **Poison messages → retry cap + dead-letter queue.** A message that
   *deterministically* crashes the handler would otherwise redeliver forever,
   blocking the queue and every replica. Cap redeliveries (e.g. `x-delivery-count`
   on a quorum queue, or a retry header) and, once exceeded, `nack(requeue:false)`
   to a **dead-letter queue** for inspection. Distinguish this from an infra crash
   (redeliver freely) — only repeated handler failures on the *same* message count
   toward the cap.
6. **Durability + prefetch.** Queues `durable`, messages persistent
   (delivery-mode 2), so a broker restart doesn't drop queued work. `BasicQos`
   prefetch small (e.g. 1) so one replica doesn't hog unacked messages and load
   spreads across competing consumers. Optional rigor: **publisher confirms** on
   the result publish, so the worker acks the request only once the broker has
   confirmed the result — closing the "published locally but the broker never got
   it" gap.
7. **Stuck-record reclaim is the backstop.** Manual ack covers worker crashes;
   the section 12 timeout/reclaim (re-queue records whose `updatedAt` is stale)
   covers the residue — a dead-lettered or genuinely lost message — so a record
   never hangs in `fetching-transcript`/`summarizing` forever.

### Results consumer & queue topology

The api **gets the transcript result back over a separate, always-on consumer —
not the HTTP request that submitted the video.** The `POST` handler already
returned `202` and its thread is gone; the result arrives asynchronously later.

- **The consumer is a hosted background service.** In the api process it runs as
  an `IHostedService`/`BackgroundService` that starts with the app, opens its own
  channel, subscribes to the results queue, and stays subscribed for the
  process's whole lifetime. It is decoupled from request handling — it processes
  `TranscriptReady`/`TranscriptUnavailable` whenever they land. (`transcript-service`
  is likewise a hosted worker consuming the requests queue.)
- **Two one-way channels, keyed by `videoId`:**

  ```
                   requests exchange           transcript.requests queue
  api  ──publish──►   (direct)   ──────────►   consumed by transcript-service
       TranscriptRequested                     (competing consumers, scale N)
                                                        │ fetch (YoutubeExplode)
                   results exchange            transcript.results queue
  api  ◄──consume──   (direct)   ◄──────────   publish TranscriptReady /
    results IHostedService                            TranscriptUnavailable
  ```

  One results queue carries both result types; discriminate by a `type` header or
  by routing keys (`transcript.ready` / `transcript.unavailable`) bound to it, and
  dispatch in the consumer. If **api** is scaled to several instances they compete
  on the results queue, so each result is handled once.
- **No RPC / correlation-id.** This is event-driven pub/sub, not request/reply:
  the `videoId` (the unique-indexed natural key) correlates a result to its
  record, and the **database is the shared state** between the `POST` handler and
  the consumer — they never hand anything to each other in memory.
- **The LLM call runs in this consumer, not in `transcript-service`.** On
  `TranscriptReady{videoId, title, transcript}` the consumer loads the record by
  `videoId`, no-ops if it's already `processed`/`failed` (idempotency), sets
  `summarizing`, calls `ISummarizer` with the **inline transcript from the
  message**, saves the summary and sets `processed` (or `failed`), then acks. The
  transcript is discarded after summarizing — only the summary is persisted
  (section 6). `TranscriptUnavailable` sets `failed` with the reason and acks.
- **The browser never touches RabbitMQ.** The consumer's DB write is what the
  client observes: its next `GET /api/summaries/{id}` poll returns the new status.
  The full path is queue → consumer → DB → poll.

---

## 5. REST API contract (proposed)

Endpoints map directly to use cases. Shapes below are a proposal to firm up in
design.

### `POST /api/summaries`

Submit a video for summarization. The server keys off the existing record's
`status` (see section 6):

| Existing record | Action | Response |
| --- | --- | --- |
| none | create with `status: new`, **publish `TranscriptRequested`** | `202 Accepted` with the record |
| `processed` | return the stored summary (FR5) | `200 OK` with the summary |
| in-flight (`new`/`fetching-transcript`/`summarizing`) | do **not** publish a duplicate; return the in-flight record (FR8) | `202 Accepted` |
| `failed` | re-queue (reset to `new`) and **republish `TranscriptRequested`** | `202 Accepted` |

**The endpoint never blocks on processing.** It does only the fast work —
validate, upsert the record, publish the message — and returns `202` in
milliseconds. It does **not** hold the request open waiting for the summary;
that would tie up a server thread for the whole fetch+LLM duration, hit
timeouts, and defeat the queue. The client observes progress afterwards via the
poll endpoint (see [Progress delivery](#progress-delivery)).

Request:

```json
{ "url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ" }
```

`200 OK` (already `processed`):

```json
{
  "id": "665f1c...",
  "videoUrl": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "status": "processed",
  "title": "Example video",
  "summary": "Short LLM-generated summary text...",
  "createdAt": "2026-07-07T12:00:00Z"
}
```

`202 Accepted` (accepted / already in flight) — client polls
`GET /api/summaries/{id}` until `status` is `processed` or `failed`:

```json
{ "id": "665f1c...", "status": "new" }
```

Error responses:

- `400 Bad Request` — `url` missing, malformed, or not a valid YouTube link
  (rejected by `VideoUrl`); wrong content-type; body too large.
- `429 Too Many Requests` — rate limit exceeded.
- `503 Service Unavailable` — the message broker is unreachable at submit time,
  so the request can't be queued.

Because processing is asynchronous, transcript-unavailable and LLM/provider
failures do **not** surface on this POST — the record is accepted (`202`) and
the failure appears later as `status: failed` (with `error`) observed via
`GET /api/summaries/{id}`. `422` (no transcript) is reflected on that record's
status, not returned from the POST.

### `GET /api/summaries`

History list of processed videos (newest first).

```json
[
  { "id": "665f1c...", "videoUrl": "...", "videoId": "dQw4w9WgXcQ",
    "title": "Example video", "createdAt": "2026-07-07T12:00:00Z" }
]
```

### `GET /api/summaries/{id}`

Single record, including its current `status` — this is the endpoint a client
polls after a `202`. The `status` carries the current progress stage
(`new` → `fetching-transcript` → `summarizing` → `processed`/`failed`, see
section 6), so the UI can render a progress indicator, not just a spinner. Poll
until `status` is `processed` (summary present) or `failed` (`error` present).
`404 Not Found` if the id is unknown.

---

## 6. Data model (proposed)

One MongoDB document per video (`ISummaryRepository` backing store). The record
is created as soon as a URL is submitted — before the summary exists — so its
lifecycle is tracked by `status`:

```json
{
  "_id": "ObjectId",
  "videoUrl": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "videoId": "dQw4w9WgXcQ",
  "status": "new | fetching-transcript | summarizing | processed | failed",
  "title": "Example video",
  "summary": "Short LLM-generated summary text...",
  "provider": "github-models (interim) | openai | anthropic | ...",
  "model": "the model id used, e.g. openai/gpt-4o-mini",
  "error": "reason, present only when status is failed",
  "createdAt": "2026-07-07T12:00:00Z",
  "updatedAt": "2026-07-07T12:00:05Z"
}
```

- `videoId` is the natural dedupe key (FR5/FR8); index it **unique** so a URL
  maps to exactly one record and concurrent submissions can't create duplicates.
- `summary`/`provider`/`model` are populated only once `status` is `processed`;
  `error` is populated only when `status` is `failed`.

### Status lifecycle

`status` doubles as the **progress stage** the UI renders. The in-flight part is
split into two observable stages (`fetching-transcript`, `summarizing`) so the
client can show *where* processing is, not just "busy". Every DB write is done by
`api` — `transcript-service` never touches Mongo; it only emits messages that
`api`'s result consumer turns into status updates.

```
                submit
        (no record) ─────► new ──────────────► fetching-transcript
                            ▲  (api publishes    │  api receives TranscriptReady
                            │   TranscriptReq.)   ▼
                            │                   summarizing
                            │              ┌────────┴────────┐
              resubmit      │    success   │                 │  no transcript / LLM error
           (re-queue → new) │              ▼                 ▼
                            └──────────── processed         failed
                                                              │
                                                              └──► resubmit
```

- **new** — record created, `TranscriptRequested` published, worker hasn't
  reported back yet. UI: "Queued…". Also the re-queue target on resubmit.
- **fetching-transcript** — `api` set this right after publishing; the transcript
  worker is fetching. UI: "Fetching transcript…" (step 1).
- **summarizing** — `api` received `TranscriptReady` and the LLM call is running.
  UI: "Summarizing…" (step 2).
- **processed** — summary ready; FR5 cache hits return from here. UI: render it.
- **failed** — transcript unavailable or provider error; `error` explains why.
  A resubmit re-queues it back to `new`.
- **In-flight = any status except `processed`/`failed`.** That's the single
  predicate the FR8 dedupe uses — a repeat submit for an in-flight URL returns
  the existing record instead of publishing a second request, so the extra
  stages don't complicate the dedupe.
- `fetching-transcript` is set optimistically at publish time. If workers are
  saturated the record can read "fetching" while really still queued; acceptable
  now. To distinguish a real queue wait, have `transcript-service` emit a
  `TranscriptFetchStarted` event and flip `new → fetching-transcript` on it —
  deferred (see [open questions](#12-open-questions)).
- Whether the full transcript text is stored on the document or re-fetched on
  demand is an [open question](#12-open-questions); the shape above stores only
  the summary.

### Progress delivery

The client shows live progress by **polling** the single-record endpoint — the
starting choice for status delivery (section 12), chosen because it needs no
extra infrastructure (the `GET /api/summaries/{id}` endpoint already exists) and
is resilient to dropped connections.

```
React (browser)                      api                         rabbitmq / transcript-service
  │  POST /api/summaries {url}         │
  │ ─────────────────────────────────►│ validate, upsert (new), publish ──► TranscriptRequested
  │  202 { id, status:"new" }          │                                          │
  │ ◄─────────────────────────────────│ (returns immediately)                     │ worker fetches
  │                                    │◄──────────── TranscriptReady ─────────────│
  │  GET /api/summaries/{id}  ×N       │ status: summarizing → call LLM             │
  │ ─────────────────────────────────►│ status: processed (summary)                │
  │  200 { status, summary? }          │                                            │
  │ ◄─────────────────────────────────│                                            │
  ▼  render stage label → then summary
```

- Poll every ~1.5 s; stop when `status` is `processed` (render the summary) or
  `failed` (render `error`). Map each interim `status` to a step label
  ("Fetching transcript…", "Summarizing…").
- Progress is **stage-based, not a percentage** — LLM completion isn't
  measurable without token streaming, which is out of scope. An indeterminate
  bar/spinner plus the current stage label is the target UX.
- **SSE is a later upgrade**, not a rewrite: `api` could expose
  `GET /api/summaries/{id}/events` (`text/event-stream`) and push each status
  change, reading the same `status` field. Deferred (section 12).

---

## 7. LLM hosting

No Azure AI Foundry access yet; final production provider is still open.

**Interim provider: GitHub Models** (free prototyping tier, no new account — auth
is a GitHub Personal Access Token with the `models: read` permission). It exposes
an **OpenAI-compatible** Chat Completions API, so the adapter is a plain
`HttpClient` call and the provider is swappable by base URL alone.

- Endpoint: `https://models.github.ai/inference` → `POST /chat/completions`
- Auth: `Authorization: Bearer <PAT>`
- Model (summarization): `openai/gpt-4o-mini` (small/cheap) — set via `LLM_MODEL`
- Request uses `messages: [{role:"system"},{role:"user"}]`; summary is at
  `choices[0].message.content`.

Because the interim provider is OpenAI-compatible, the same
`OpenAiCompatibleSummarizer : ISummarizer` adapter also targets **Ollama**
(local, zero-cost, `http://localhost:11434/v1`), **Groq**, or **OpenAI** by
changing only `LLM_BASE_URL` / `LLM_API_KEY` / `LLM_MODEL` — no code change.

Caveats: GitHub Models is rate-limited and, per its terms, **prototyping only —
not production**. A `429` maps to a `failed` record (FR8) the client can retry.

Switch to Azure AI Foundry (or a paid provider) later purely by adding a new
`Infrastructure` adapter — the `Application` layer must not change. Revisit once
Foundry access is available.

### Setting up GitHub Models

GitHub runs the inference — this is not self-hosting a model, it's a free,
rate-limited, OpenAI-compatible endpoint for prototyping.

1. **Create a token.** Generate a GitHub **fine-grained Personal Access Token**
   (Settings → Developer settings → Personal access tokens → Fine-grained
   tokens) with the **`Models: read-only`** account permission. This is *not* a
   Copilot or ChatGPT subscription — a consumer chat plan cannot be used
   programmatically. The token (starts with `github_pat_…`) is the `LLM_API_KEY`.
2. **Pick a model.** Browse the catalog at
   <https://github.com/marketplace/models>. Set the id as `LLM_MODEL`
   (e.g. `openai/gpt-4o-mini` for a small/cheap default).
3. **Point the adapter at it** via the section 8 variables:

   | Variable | Value |
   | --- | --- |
   | `LLM_BASE_URL` | `https://models.github.ai/inference` |
   | `LLM_API_KEY` | the `github_pat_…` token |
   | `LLM_MODEL` | e.g. `openai/gpt-4o-mini` |

4. **Smoke-test the token** before wiring any adapter — this proves the PAT,
   endpoint, and model id in one call (it's OpenAI-compatible, so the path is
   `/chat/completions`). It also de-risks the whole LLM work stream: the adapter
   later does exactly this.

   Bash:

   ```bash
   curl "https://models.github.ai/inference/chat/completions" \
     -H "Authorization: Bearer $GITHUB_PAT" \
     -H "Content-Type: application/json" \
     -d '{ "model": "openai/gpt-4o-mini",
           "messages": [{ "role": "user", "content": "Reply with one word: pong" }] }'
   ```

   Windows PowerShell (reads the token from the git-ignored `.env`, prints only
   the model's reply — never the token):

   ```powershell
   $cfg = @{}
   Get-Content .env | Where-Object { $_ -match '^\s*[^#].*=' } | ForEach-Object {
     $k,$v = $_ -split '=',2; $cfg[$k.Trim()] = $v.Trim()
   }
   $body = @{
     model    = $cfg['LLM_MODEL']
     messages = @(@{ role = 'user'; content = 'Reply with one word: pong' })
   } | ConvertTo-Json -Depth 5
   Invoke-RestMethod -Method Post `
     -Uri "$($cfg['LLM_BASE_URL'])/chat/completions" `
     -Headers @{ Authorization = "Bearer $($cfg['LLM_API_KEY'])" } `
     -ContentType 'application/json' -Body $body |
     ForEach-Object { $_.choices[0].message.content }
   ```

   Interpreting the result:

   | Outcome | Meaning |
   | --- | --- |
   | a one-word completion comes back | ✅ token, endpoint, and model all good |
   | `401 Unauthorized` | token wrong/expired or missing `Models: read-only` |
   | `404 Not Found` | wrong model id or base URL |
   | `429 Too Many Requests` | token is valid — just the free-tier rate limit |

   The response shape — `model` + `messages[]` in, summary at
   `choices[0].message.content` out — is exactly the contract
   `OpenAiCompatibleSummarizer` implements (this section, top).

   > **Status:** verified against `openai/gpt-4o-mini` (resolved to
   > `gpt-4o-mini-2024-07-18`) — the endpoint returns completions with the
   > current PAT.

---

## 8. Configuration & secrets

Runtime configuration the app will need. These belong in configuration/secret
stores, never in source. Under Docker they are injected as environment
variables via Compose, sourced from a git-ignored `.env` (see `.env.example`):

- `MONGO_CONNECTION_STRING` — MongoDB connection string. When run via Compose
  this points at the `mongo` service (e.g. `mongodb://mongo:27017`).
- `RABBITMQ_URI` — RabbitMQ connection URI, used by both `api` and
  `transcript-service`. Under Compose this points at the `rabbitmq` service
  (e.g. `amqp://guest:guest@rabbitmq:5672`).
- `LLM_BASE_URL` — OpenAI-compatible endpoint base. Interim:
  `https://models.github.ai/inference`. Swap to point at Ollama/Groq/OpenAI.
- `LLM_API_KEY` — credential for the endpoint. For the interim GitHub Models
  provider this is a **GitHub PAT with `models: read`** (not a Claude/ChatGPT
  subscription — those can't be used programmatically). Any placeholder for Ollama.
- `LLM_MODEL` — model id (e.g. `openai/gpt-4o-mini`).

`.env.example` is committed with placeholder values and no secrets; each
developer copies it to `.env` and fills in their own key.

---

## 9. Running with Docker

The whole app runs with a single command via Docker Compose:

```
docker compose up
```

`docker-compose.yml` (repo root) defines five services:

| Service | Description |
| --- | --- |
| `mongo` | Official MongoDB image; named volume for persistence; the store behind `MongoSummaryRepository`. |
| `rabbitmq` | Official RabbitMQ image (management plugin variant); the broker between `api` and `transcript-service`. Own container. |
| `transcript-service` | Built from `src/TranscriptService/Dockerfile`. Depends on `rabbitmq`. Runs YoutubeExplode; **scalable** (`--scale transcript-service=N`). No ports exposed — talks only to the broker. |
| `api`   | Built from `src/Api/Dockerfile` (multi-stage .NET build). Depends on `mongo` + `rabbitmq`. Exposes the HTTP API. |
| `web`   | Built from `web/Dockerfile`. Serves the React client and proxies to `api`. |

Notes:

- Configuration is passed through environment variables from `.env`
  (see section 8) — no secrets baked into images.
- The `mongo` and `rabbitmq` services can be brought up on their own
  (`docker compose up mongo rabbitmq`) to back local `dotnet run` development
  before the api/transcript/web images exist.
- Each Dockerfile lands with its owning project's milestone (section 11);
  until then this section is the target shape, and only the `mongo` and
  `rabbitmq` services are runnable.
- `transcript-service` is the scale-out point: run several replicas as competing
  consumers on the transcript-request queue when fetch throughput is the
  constraint.

---

## 10. Git workflow

The project uses **GitFlow** (`main`, `develop`, `feature/*`, `release/*`,
`hotfix/*`, `bugfix/*`). See [`CLAUDE.md`](../CLAUDE.md#git-workflow--gitflow)
for the authoritative rules and branch-naming conventions.

---

## 11. Milestones / build order

Suggested ordered slices, inner layers first:

- **M1 — Domain**: value objects and entities (`VideoUrl`/`VideoId`, `Video`,
  `Transcript`, `Summary`); unit tested.
- **M2 — Application**: use cases (`SummarizeVideo` submit + transcript-result
  handler, `GetSummaryHistory`) against `ITranscriptRequestPublisher`/
  `ISummarizer`/`ISummaryRepository`; the `Contracts` integration events; unit
  tested with faked dependencies.
- **M3 — Infrastructure**: adapters — `RabbitMqTranscriptRequestPublisher`, an
  OpenAI-compatible `ISummarizer` (interim: GitHub Models, section 7),
  `MongoSummaryRepository`, and the RabbitMQ consumer plumbing; integration
  tested. Add `docker-compose.yml` with the `mongo` and `rabbitmq` services so
  integration tests and local dev have a database and broker with one command.
- **M4 — Transcript service**: standalone `TranscriptService` worker —
  `YoutubeExplodeTranscriptFetcher` behind a `TranscriptRequested` consumer that
  publishes `TranscriptReady`/`TranscriptUnavailable`; idempotent, with
  retry/dead-letter. Add `src/TranscriptService/Dockerfile` and the
  `transcript-service` Compose service.
- **M5 — Api**: minimal API endpoints + DI wiring + the transcript-result
  consumer in the composition root. Add `src/Api/Dockerfile` and the `api`
  Compose service. End-to-end: submit → queue → transcript → LLM → `processed`.
- **M6 — Web**: React client built with **Material UI (MUI)** — submit form,
  progress view, summary view, history list.
  Add `web/Dockerfile` and the `web` Compose service, completing the
  one-command `docker compose up` run.

---

## 12. Open questions

- **LLM provider**: interim is **GitHub Models** (section 7); open question is
  the production provider and when to move to Azure AI Foundry.
- **Transcript persistence**: store the full transcript on the document, or
  re-fetch on demand and persist only the summary.
- **Auth / multi-user scope**: currently none; history is global. Revisit if
  per-user history is needed.
- **Processing execution**: *Resolved* — transcript fetching is a separate
  `transcript-service` reached asynchronously over RabbitMQ; summarization runs
  in an `api`-side consumer reacting to `TranscriptReady`. Whether to also split
  summarization into its own worker is deferred until LLM throughput demands it.
- **Messaging reliability**: *Approach settled* — at-least-once with manual ack,
  idempotent handlers, retry cap + dead-letter queue (section 4, "Message
  delivery & acknowledgment"). Still open: concrete retry cap / DLQ policy,
  exchange/queue topology, quorum-vs-classic queues, and whether results carry
  the full transcript in the message or a reference.
- **Status delivery**: *Resolved for now* — the client **polls**
  `GET /api/summaries/{id}` (~1.5 s) and renders the `status` as a progress stage
  (section 6, "Progress delivery"). SSE (`text/event-stream`) is a later,
  additive upgrade that reads the same `status` field. Open sub-question: whether
  to add a `TranscriptFetchStarted` event so `new` (queued) is distinguishable
  from `fetching-transcript` (worker active) under load.
- **Rate-limit thresholds**: concrete per-IP/per-user limits and body-size cap
  to configure in `AddRateLimiter`.
- **Stuck in-flight records**: a crash or a lost message could leave a record in
  `fetching-transcript`/`summarizing` forever — need a timeout/reclaim (e.g.
  re-queue records whose `updatedAt` is older than N minutes).
