# youtube-reader — Development Plan

Actionable build plan derived from [`specification.md`](./specification.md) and
[`CLAUDE.md`](../CLAUDE.md). The spec defines *what* to build and *why*; this
document sequences *how* — as GitFlow feature branches with concrete tasks,
deliverables, and acceptance criteria.

**Authority:** `CLAUDE.md` > `specification.md` > this plan. If they conflict,
the higher source wins; update this plan rather than diverging.

---

## Architecture decision: transcript fetching is a separate service

YoutubeExplode does **not** run inside the `api` container. It runs in its own
deployable, `transcript-service`, and `api` talks to it **asynchronously over
RabbitMQ** — never direct HTTP. Rationale: the transcript layer is the fragile,
rate-limited part (reverse-engineered YouTube scraping), so isolating it lets us
**scale it independently** (multiple replicas as competing consumers) and keep
its failures from taking down the API.

**Message flow (async, event-driven):**

```
POST /api/summaries
  → api: create record (new), publish TranscriptRequested{videoId,url}, return 202

transcript-service  (N replicas, competing consumers)
  → consume TranscriptRequested → YoutubeExplode
  → publish TranscriptReady{videoId,title,transcript}
         or TranscriptUnavailable{videoId,reason}

api result consumer
  → consume TranscriptReady → LLM summarize (ISummarizer) → DB: processed
  → consume TranscriptUnavailable → DB: failed

web → polls GET /api/summaries/{id} until processed | failed
```

Summarization stays **inside `api`** (an api-side consumer), not a third worker —
splitting it out is deferred until LLM throughput demands it (spec §12).

**Hosting:** `rabbitmq` is its own container alongside `mongo` (spec §9).

---

## Current state

Already in the repo (scaffold only — no application code yet):

- `docker-compose.yml` — `mongo` and `rabbitmq` services live; `transcript-service`,
  `api`, `web` commented out until M4/M5/M6.
- `.env.example` — `MONGO_CONNECTION_STRING`, `RABBITMQ_URI`, `LLM_BASE_URL`,
  `LLM_API_KEY`, `LLM_MODEL`.
- `docs/specification.md`, `CLAUDE.md`.

Not yet present: `src/`, `web/`, `tests/`, the .NET solution, any Dockerfiles.

---

## Requested features → work streams

| Requested feature | Where it lives | Phase |
| --- | --- | --- |
| Host LLM model in GitHub | GitHub Models setup + `OpenAiCompatibleSummarizer` (`Infrastructure`) | P0 + P3 |
| DB service | `mongo` container (done) + `MongoSummaryRepository` (`Infrastructure`) | P3 |
| Transcript service (separate, RabbitMQ) | `rabbitmq` container + `TranscriptService` worker + messaging adapters | P0 + P3 + P4 |
| API service | `Api` minimal API + composition root + result consumer | P5 |
| UI | `web/` React client | P6 |

`Domain` (P1) and `Application` (P2) are prerequisites the spec requires but the
feature list doesn't name — they hold the model, use cases, and messaging
abstractions everything else depends on.

**Dependency chain:** P0 → P1 → P2 → P3 → P4 → P5 → P6.
P3's adapters (Mongo repo, LLM summarizer, RabbitMQ publisher/consumer plumbing)
are independent of each other and can be built in parallel once P2 lands. P4
(transcript worker) and P5 (api) both consume the messaging built in P3.

---

## Phase 0 — Project setup & prerequisites

**Branch:** `feature/solution-scaffold`

**Goal:** an empty-but-compiling solution, a validated LLM endpoint, and a
reachable Mongo + RabbitMQ, so every later phase has somewhere to land.

**Tasks:**

1. Create the .NET solution and projects, dependencies pointing inward only:
   - `src/Domain` (no dependencies)
   - `src/Contracts` (integration-event DTOs; no dependencies)
   - `src/Application` → `Domain`, `Contracts`
   - `src/Infrastructure` → `Application`
   - `src/Api` → `Application` + `Infrastructure` (composition root)
   - `src/TranscriptService` → `Domain` + `Contracts` (+ its own YoutubeExplode /
     RabbitMQ infra)
   - Enforce the dependency rule: `Domain`/`Application` reference **no**
     YoutubeExplode, LLM SDK, MongoDB, or RabbitMQ client packages.
2. Test projects: `tests/Domain.Tests`, `tests/Application.Tests`,
   `tests/Infrastructure.Tests`, `tests/TranscriptService.Tests` (xUnit). All
   unit tests for now — no real Mongo/RabbitMQ/GitHub Models in the suite
   (see `CLAUDE.md`).
3. **Host LLM model in GitHub (setup half):** create a GitHub fine-grained PAT
   with `Models: read-only` (spec §7); fill `LLM_API_KEY` in a git-ignored
   `.env`; smoke-test with the bash/PowerShell snippet in spec §7 before any
   adapter exists. GitHub runs the inference — nothing is self-hosted; the token
   is just the HTTP credential. **✅ Done:** verified against `openai/gpt-4o-mini`
   — the endpoint returns completions with the current PAT.
4. Confirm `docker compose up mongo rabbitmq` starts a reachable Mongo (`27017`)
   and RabbitMQ (`5672`, management UI `15672`).

**Acceptance:** `dotnet build` + `dotnet test` run green (empty); the smoke test
returns a completion (✅ done); Mongo and RabbitMQ are reachable.

---

## Phase 1 — Domain (M1)

**Branch:** `feature/domain-model`

**Goal:** the core model, framework-free and fully unit-tested — where
YouTube-URL validity is *defined*.

**Tasks:**

1. `VideoUrl` / `VideoId` — owns all YouTube URL parsing/validation (FR1); single
   source of truth. Handle `watch?v=`, `youtu.be/`, `shorts/` forms.
2. `Transcript` — non-empty invariant; wraps joined caption text.
3. `Summary` — LLM output + `provider`/`model` provenance.
4. `Video` entity — owns the `status` lifecycle transitions
   (`new → fetching-transcript → summarizing → processed | failed`, resubmit
   `failed → new`) so illegal transitions are impossible.

**Acceptance:** unit tests cover valid/invalid URLs across all forms,
empty-transcript rejection, and every legal/illegal status transition. No
framework references.

---

## Phase 2 — Application (M2) + messaging contracts

**Branch:** `feature/application-usecases`

**Goal:** use cases and the messaging boundary expressed against interfaces only —
no RabbitMQ, no concrete adapters. Async flow proven with fakes.

**Tasks:**

1. `Contracts` integration events: `TranscriptRequested`, `TranscriptReady`,
   `TranscriptUnavailable` (plain schemas, no logic).
2. Boundary interfaces (spec §3/§4): `ITranscriptRequestPublisher`,
   `ISummarizer`, `ISummaryRepository`. Note the `ITranscriptFetcher` boundary
   now lives inside `TranscriptService` (P4), not here.
3. `SummarizeVideo` (**submit path**) — §6 state machine: dedupe on `videoId`
   (FR5/FR8), create-as-`new`, don't re-publish for in-flight records, return
   cached `processed`, re-queue `failed`; publishes `TranscriptRequested`.
4. **Transcript-result handler** — the use case that reacts to `TranscriptReady`
   (→ call `ISummarizer` → `processed`) and `TranscriptUnavailable` (→ `failed`).
   Must be **idempotent**: a redelivered result for an already-`processed` video
   is a no-op.
5. `GetSummaryHistory` — newest-first list (FR6).

**Acceptance:** unit tests cover cache-hit, in-flight-dedupe, re-queue-failed,
transcript-ready→processed, transcript-unavailable→failed, and duplicate-result
idempotency — all against in-memory fakes, no real I/O or broker.

**Resolves open question:** "processing execution" — async worker via RabbitMQ
(spec §12), not inline.

---

## Phase 3 — Infrastructure (M3): DB, LLM, RabbitMQ

**Branch:** `feature/infrastructure-adapters` (adapters are independent; may split
per adapter).

**Goal:** implement the boundary interfaces against real services, verified with
**unit tests against mocks/fakes only** — no live Mongo/RabbitMQ/GitHub Models in
the test suite for now (see `CLAUDE.md`). Covers the **DB service**, the
**LLM-in-GitHub** adapter, and the **RabbitMQ** plumbing the transcript service
and api both build on.

### 3a — DB service: `MongoSummaryRepository : ISummaryRepository`
1. Map the §6 document; configure the client per the `mongodb-connection` skill.
2. **Unique index on `videoId`** so concurrent submits can't create duplicates
   (FR5/FR8) — the DB enforces dedupe, not just app code.
3. create / get-by-id / get-by-videoId / list-newest / status-update.
4. Unit-test the mapping/query logic against a mocked driver; the unique-index
   constraint itself is not verified by an automated test for now (Mongo can't
   enforce it without a real database).

### 3b — LLM in GitHub: `OpenAiCompatibleSummarizer : ISummarizer`
1. `HttpClient`-based (via `IHttpClientFactory`), configured only by
   `LLM_BASE_URL`/`LLM_API_KEY`/`LLM_MODEL` — no provider name in code.
2. POST `/chat/completions`, `messages:[{system},{user}]`, read
   `choices[0].message.content` (spec §7).
3. Map provider failure / `429` to a `failed` outcome, not a throw.
4. Unit-test against a mocked `HttpMessageHandler` (success, `429`, malformed
   response) — no live call to GitHub Models in the test suite.

### 3c — RabbitMQ: `RabbitMqTranscriptRequestPublisher` + consumer plumbing
1. `RabbitMqTranscriptRequestPublisher : ITranscriptRequestPublisher` — publishes
   `TranscriptRequested`. Durable queues, persistent messages (delivery-mode 2).
2. Consumer host/base used by both api (results) and transcript-service
   (requests), implementing the **at-least-once / manual-ack** contract (spec §4
   "Message delivery & acknowledgment"): auto-ack **off**, **ack after publishing
   the result** (`consume → handle → publish → ack`), `BasicQos` prefetch = 1,
   retry cap + **dead-letter queue** for poison messages, `nack(requeue:false)`
   past the cap. Connection via `RABBITMQ_URI`.
3. Unit-test the ack/nack/retry-cap decision logic in isolation (mocked channel).
   Redelivery and dead-lettering are real-broker behaviors — not covered by an
   automated test for now; verify manually via `docker compose up rabbitmq` if
   needed before P4/P5 depend on this plumbing.

**Acceptance:** each adapter's unit tests pass against mocks/fakes; LLM-`429` →
`failed` (unit-tested); ack-after-publish and retry-cap decision logic
unit-tested. Real-service behaviors (unique-index enforcement, cross-connection
publish→consume, redelivery-on-crash, dead-lettering) are **not** covered by
automated tests in this phase — deferred until integration tests are
reintroduced.

**Resolves open question:** "transcript persistence" — store **only the summary**
on the document (spec §6). Whether `TranscriptReady` carries the full transcript
text or a reference is decided here (default: inline in the message).

---

## Phase 4 — Transcript service (M4)

**Branch:** `feature/transcript-service`

**Goal:** the standalone, independently-scalable worker — the heart of this
architecture change.

**Tasks:**

1. `TranscriptService` worker as a hosted `BackgroundService` (a Worker Service
   host, not a web host): subscribes to the requests queue and consumes
   `TranscriptRequested`.
2. `YoutubeExplodeTranscriptFetcher : ITranscriptFetcher` — two YoutubeExplode
   calls (metadata + captions) joined to one string (spec §4). Reuse one
   `HttpClient` via `IHttpClientFactory`.
3. Empty manifest / no matching language → publish `TranscriptUnavailable` **and
   ack** — it is a handled success, not a nack/requeue (else a caption-less video
   loops forever).
4. Success → publish `TranscriptReady{videoId,title,transcript}`, **then ack**.
5. **Ack after publish (at-least-once):** `consume → fetch → publish → ack`, never
   ack first. A crash before ack must let the broker redeliver the request (to
   another replica or on restart) so no work is lost — the record stays
   `fetching-transcript` until the redelivery completes (spec §4).
6. **Idempotency:** a redelivered request for an already-handled video is safe —
   re-fetching is read-only, and the api result consumer no-ops a duplicate
   result (spec §6). Transient YouTube errors retried; poison messages
   dead-lettered past the cap (reuse P3c plumbing).
7. `src/TranscriptService/Dockerfile`; **uncomment `transcript-service`** in
   `docker-compose.yml`.
8. **Carried forward from the P3 final review — resolve before wiring the real
   consumer loop:** P3's `ManualAckPolicy`/`ManualAckDispatcher` (reused here) take
   a `redeliveryCount` as input, but `RabbitMqTopology`'s queue is a plain durable
   classic queue with no `x-delivery-count` and no dead-letter-exchange
   arguments — nothing in P3 actually supplies a truthful count. Decide here
   (quorum queue with `x-delivery-count`, or DLX-based counting via `x-death`)
   and declare the matching queue arguments in the same task that wires the
   `BasicConsumeAsync` loop, or a transient failure will requeue forever and
   never reach the cap. `AckDecision.DeadLetter` also currently just drops the
   message (`nack(requeue:false)` with no DLX declared) until this is resolved.

**Acceptance:** `YoutubeExplodeTranscriptFetcher` and the ack/publish sequencing
are unit-tested against mocks (captioned, no-caption, transient-error cases). The
following are **manual smoke checks** via `docker compose up rabbitmq
transcript-service`, not automated tests, and can be done ad hoc rather than
gating the merge: a `TranscriptRequested` produces the correct result for both a
captioned and a no-caption video; killing a worker mid-fetch redelivers the
request and the video still completes; `--scale transcript-service=3` spreads
requests across replicas; a poison message dead-letters after the cap.

---

## Phase 5 — API service (M5)

**Branch:** `feature/api-endpoints`

**Goal:** the HTTP surface + composition root + the result consumer, wiring
P1–P4 together end-to-end.

**Tasks:**

1. Minimal API endpoints (spec §5), documented via the
   `aspnet-minimal-api-openapi` skill:
   - `POST /api/summaries` — 200 (cached) / 202 (accepted or in-flight) / 400 /
     429 / 503 (broker unreachable). Publishes `TranscriptRequested`; transcript
     and LLM failures surface **asynchronously** as `failed`, not on this POST.
   - `GET /api/summaries` — history, newest first.
   - `GET /api/summaries/{id}` — single record + status (the poll target); 404.
2. **Status stages as progress signal** (spec §6): `POST` upserts `new` and
   returns `202` **without blocking**; `api` sets `fetching-transcript` right
   after publishing. The result consumer (task 3) advances `summarizing` →
   `processed`/`failed`. Every status write is done by `api` — `transcript-service`
   never touches Mongo. `GET /api/summaries/{id}` exposes the current stage so the
   UI can render progress.
3. **Transcript-result consumer** as an `IHostedService`/`BackgroundService` in
   the api process (spec §4 "Results consumer & queue topology") — starts with the
   app, subscribes to the results queue for the process lifetime, **separate from
   request handling**. Consumes `TranscriptReady`/`TranscriptUnavailable`,
   correlates by `videoId`, runs the P2 result handler (`summarizing` → LLM on the
   inline transcript → `processed`, or `failed`). Same manual-ack contract as
   P3c/P4 — ack after the DB write; a result for a video already
   `processed`/`failed` is an idempotent **no-op** (handles duplicate delivery).
4. **Request validation at the boundary** (FR7): reject missing/empty `url`,
   non-YouTube URL (via `VideoUrl`), wrong content-type, oversized body → `400`.
5. **Abuse/DDoS protection** (FR9): `AddRateLimiter` per-IP (→ `429`), max body
   size, request timeouts.
6. Composition root: DI of Mongo repo + LLM summarizer + RabbitMQ publisher +
   result consumer + `IHttpClientFactory`; config from env (spec §8). Health check.
7. `src/Api/Dockerfile`; **uncomment `api`** in `docker-compose.yml`.

**Acceptance:** `docker compose up mongo rabbitmq transcript-service api` runs the
full backend; a real URL flows submit → queue → transcript → LLM → `processed`,
observed via polling; cache hit returns 200 without re-processing; malformed
input → 400; over-limit → 429.

**Decisions to make here** (spec §12): concrete per-IP rate-limit numbers and
body-size cap; timeout/reclaim for stuck in-flight records (`fetching-transcript`/
`summarizing`) — re-queue when `updatedAt` older than N minutes (also covers a
lost message).

---

## Phase 6 — UI (M6)

**Branch:** `feature/web-client`

**Goal:** the React (TypeScript) client — a **thin** client over the API, no
duplicated business logic (spec §3), built with **Material UI (MUI)**. Completes
`docker compose up`. Design:
`docs/superpowers/specs/2026-07-09-video-detail-page-design.md`.

**Tasks:**

1. Scaffold `web/` (React + TypeScript) and add MUI + router:
   ```
   npm install @mui/material @emotion/react @emotion/styled react-router-dom
   ```
   `@mui/material` is the component library; `@emotion/react` + `@emotion/styled`
   are its default styling engine (peer deps). `react-router-dom` backs the
   `/` and `/video/:id` routes (task 3). Wrap the app in a MUI `ThemeProvider` +
   `CssBaseline`. Optional later: `@mui/icons-material` for icons. MUI is
   presentation only — no business logic (spec §3).
2. **Main page (`/`)** — submit form (MUI `TextField` URL + `Button`) +
   history list. On `POST /api/summaries` response (`200` or `202`), navigate
   to `/video/{id}` — both outcomes converge on the detail page, which renders
   correctly from whatever `status` it first polls.
3. **Detail page (`/video/:id`)** — polls `GET /api/summaries/{id}` (~1.5 s,
   spec §6) and renders exactly one view per `status`:
   - `new` / `fetching-transcript` / `summarizing` → full-page MUI
     `CircularProgress` + stage label ("Queued…", "Fetching transcript…",
     "Summarizing…"). Indeterminate — no percentage (spec §6).
   - `processed` → `VideoPlayer` (task 4) + summary `Card` (title, summary)
     shown together. Stop polling.
   - `failed` → MUI `Alert` with `error`, no player, resubmit affordance.
     Stop polling.
4. **`VideoPlayer` component** — presentational, responsive 16:9 wrapper
   around `<iframe src="https://www.youtube.com/embed/{videoId}">`. No player
   library. Takes `videoId` as its only prop.
5. **History list** — `GET /api/summaries`, newest first, as a MUI
   `List`/`Card` grid; each entry links to `/video/{id}`.
6. Surface API errors with MUI `Alert`/`Snackbar`: 400 (bad URL), 429 (rate
   limited), 503 (broker down), and the async `failed` state (no transcript /
   provider error).
7. `web/Dockerfile` proxying to `api`; **uncomment `web`** in `docker-compose.yml`.

**Acceptance:** `docker compose up` (all services) brings up the full app; a user
pastes a link on `/`, is taken to `/video/{id}`, watches it process, then sees
the player and summary together, and can find it again from history — with no
summarization/parsing logic in the frontend.

---

## Milestone ↔ phase map

| Spec §11 milestone | Plan phase | GitFlow branch |
| --- | --- | --- |
| (pre-work) | P0 | `feature/solution-scaffold` |
| M1 Domain | P1 | `feature/domain-model` |
| M2 Application | P2 | `feature/application-usecases` |
| M3 Infrastructure | P3 | `feature/infrastructure-adapters` |
| M4 Transcript service | P4 | `feature/transcript-service` |
| M5 Api | P5 | `feature/api-endpoints` |
| M6 Web | P6 | `feature/web-client` |

Each `feature/*` branches off `develop` and merges back into `develop` when its
acceptance criteria pass (GitFlow, `CLAUDE.md`). Cut a `release/*` off `develop`
for the first tagged `main` release once P6 lands.

---

## Open questions to resolve while building

Carried from spec §12 — each has a chosen starting point above; revisit only if a
constraint changes:

- **Production LLM provider / Azure AI Foundry** — interim GitHub Models; new
  `Infrastructure` adapter later, no `Application` change.
- **Transcript persistence** — start: store summary only; `TranscriptReady`
  carries the transcript inline (P3/P4).
- **Messaging reliability** — retry policy, dead-letter queue, consumer
  idempotency, exchange/queue topology (P3c/P4).
- **Auth / multi-user** — none; history is global.
- **Processing execution** — resolved: async transcript worker over RabbitMQ +
  api-side summarization consumer.
- **Status delivery** — resolved: **polling** with stage-based progress from the
  `status` field (P5 writes stages, P6 renders them); SSE an additive upgrade
  later. Optional `TranscriptFetchStarted` event to separate queued from actively
  fetching under load — deferred.
- **Rate-limit thresholds** — decide concrete numbers in P5.
- **Stuck in-flight records** — add timeout/reclaim in P5 (also covers lost messages).
