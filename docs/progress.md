# youtube-reader — Progress Tracker

Tracks implementation status against [`plan.md`](./plan.md). Update checkboxes as
tasks land; a phase's summary checkbox flips only when **all** its tasks and
acceptance criteria are checked.

## Summary

| Phase | Branch | Status |
| --- | --- | --- |
| P0 — Project setup & prerequisites | `feature/solution-scaffold` | [ ] Not started |
| P1 — Domain (M1) | `feature/domain-model` | [ ] Not started |
| P2 — Application (M2) + messaging contracts | `feature/application-usecases` | [ ] Not started |
| P3 — Infrastructure (M3): DB, LLM, RabbitMQ | `feature/infrastructure-adapters` | [ ] Not started |
| P4 — Transcript service (M4) | `feature/transcript-service` | [ ] Not started |
| P5 — API service (M5) | `feature/api-endpoints` | [ ] Not started |
| P6 — UI (M6) | `feature/web-client` | [ ] Not started |

---

## P0 — Project setup & prerequisites

**Branch:** `feature/solution-scaffold`

- [x] .NET solution + projects created, dependencies pointing inward only
      (`Domain`, `Contracts`, `Application`, `Infrastructure`, `Api`,
      `TranscriptService`) — target framework **.NET 10**
- [ ] Test projects scaffolded: `tests/Domain.Tests`, `tests/Application.Tests`,
      `tests/Infrastructure.Tests`, `tests/TranscriptService.Tests` (xUnit) —
      **unit tests only for now**, no real Mongo/RabbitMQ/GitHub Models in the
      suite
- [x] GitHub Models PAT created, `.env` filled, smoke test verified against
      `openai/gpt-4o-mini`
- [ ] `docker compose up mongo rabbitmq` confirmed reachable (Mongo `27017`,
      RabbitMQ `5672`/`15672`)

**Acceptance:**
- [ ] `dotnet build` + `dotnet test` run green (empty)
- [x] LLM smoke test returns a completion
- [ ] Mongo and RabbitMQ reachable

---

## P1 — Domain (M1)

**Branch:** `feature/domain-model`

- [ ] `VideoUrl` / `VideoId` — YouTube URL parsing/validation (`watch?v=`,
      `youtu.be/`, `shorts/`)
- [ ] `Transcript` — non-empty invariant
- [ ] `Summary` — LLM output + provider/model provenance
- [ ] `Video` entity — status lifecycle transitions
      (`new → fetching-transcript → summarizing → processed | failed`,
      `failed → new` resubmit)

**Acceptance:**
- [ ] Unit tests cover valid/invalid URLs across all forms
- [ ] Empty-transcript rejection tested
- [ ] Every legal/illegal status transition tested
- [ ] No framework references in `Domain`

---

## P2 — Application (M2) + messaging contracts

**Branch:** `feature/application-usecases`

- [ ] `Contracts`: `TranscriptRequested`, `TranscriptReady`,
      `TranscriptUnavailable`
- [ ] Boundary interfaces: `ITranscriptRequestPublisher`, `ISummarizer`,
      `ISummaryRepository`
- [ ] `SummarizeVideo` (submit path) — dedupe, create-as-`new`, no re-publish
      in-flight, cached `processed`, re-queue `failed`
- [ ] Transcript-result handler — `TranscriptReady` → `processed`,
      `TranscriptUnavailable` → `failed`, idempotent on redelivery
- [ ] `GetSummaryHistory` — newest-first list

**Acceptance:**
- [ ] Unit tests: cache-hit, in-flight-dedupe, re-queue-failed,
      transcript-ready→processed, transcript-unavailable→failed,
      duplicate-result idempotency — all against in-memory fakes

---

## P3 — Infrastructure (M3): DB, LLM, RabbitMQ

**Branch:** `feature/infrastructure-adapters`

**Test scope for this phase: unit tests against mocks/fakes only** — no live
Mongo/RabbitMQ/GitHub Models in the suite (see `CLAUDE.md`). Real-service
behaviors below are deferred until integration tests are reintroduced.

### 3a — `MongoSummaryRepository : ISummaryRepository`
- [ ] Document mapping + client config (`mongodb-connection` skill)
- [ ] Unique index on `videoId`
- [ ] create / get-by-id / get-by-videoId / list-newest / status-update
- [ ] Unit test mapping/query logic against a mocked driver (index enforcement
      itself not covered — needs a real DB)

### 3b — `OpenAiCompatibleSummarizer : ISummarizer`
- [ ] `HttpClient`-based (`IHttpClientFactory`), config via
      `LLM_BASE_URL`/`LLM_API_KEY`/`LLM_MODEL`
- [ ] POST `/chat/completions`, read `choices[0].message.content`
- [ ] Provider failure / `429` → `failed` outcome, not a throw
- [ ] Unit test against a mocked `HttpMessageHandler` (success/429/malformed)

### 3c — RabbitMQ plumbing
- [ ] `RabbitMqTranscriptRequestPublisher : ITranscriptRequestPublisher`
      (durable queues, persistent messages)
- [ ] Shared consumer host: manual-ack, `consume → handle → publish → ack`,
      `BasicQos` prefetch = 1, retry cap + dead-letter queue
- [ ] Unit test ack/nack/retry-cap decision logic against a mocked channel
      (real redelivery/dead-lettering not covered — needs a real broker)

**Acceptance:**
- [ ] Each adapter's unit tests pass against mocks/fakes
- [ ] LLM `429` → `failed` (unit-tested)
- [ ] Ack-after-publish and retry-cap logic unit-tested

---

## P4 — Transcript service (M4)

**Branch:** `feature/transcript-service`

- [ ] `TranscriptService` worker (`BackgroundService`, Worker Service host)
      consumes `TranscriptRequested`
- [ ] `YoutubeExplodeTranscriptFetcher : ITranscriptFetcher` (metadata +
      captions, shared `HttpClient`)
- [ ] Empty manifest / no matching language → publish `TranscriptUnavailable`
      **and ack**
- [ ] Success → publish `TranscriptReady`, then ack
- [ ] Ack-after-publish ordering enforced (never ack first)
- [ ] Idempotency on redelivered requests; poison messages dead-lettered
- [ ] `src/TranscriptService/Dockerfile`; `transcript-service` uncommented in
      `docker-compose.yml`

**Acceptance:**
- [ ] `YoutubeExplodeTranscriptFetcher` + ack/publish sequencing unit-tested
      against mocks (captioned, no-caption, transient-error cases)
- [ ] Manual smoke checks only (not gating, not automated): captioned/no-caption
      videos produce correct results; killing a worker mid-fetch redelivers the
      request; `--scale transcript-service=3` spreads requests across replicas;
      a poison message dead-letters after the cap

---

## P5 — API service (M5)

**Branch:** `feature/api-endpoints`

- [ ] `POST /api/summaries` — 200 / 202 / 400 / 429 / 503
- [ ] `GET /api/summaries` — history, newest first
- [ ] `GET /api/summaries/{id}` — single record + status; 404
- [ ] Status stages written by `api` only (`new` → `fetching-transcript` →
      … ), never by `transcript-service`
- [ ] Transcript-result consumer (`BackgroundService`) — separate from
      request handling, correlates by `videoId`, idempotent no-op on
      already-`processed`/`failed`
- [ ] Request validation at the boundary (FR7): missing/empty `url`,
      non-YouTube URL, wrong content-type, oversized body → 400
- [ ] Rate limiting (FR9): per-IP `AddRateLimiter`, max body size, timeouts
- [ ] Composition root: DI wiring, config from env, health check
- [ ] `src/Api/Dockerfile`; `api` uncommented in `docker-compose.yml`
- [ ] Decide: concrete rate-limit numbers, body-size cap, stuck-in-flight
      timeout/reclaim threshold

**Acceptance:**
- [ ] `docker compose up mongo rabbitmq transcript-service api` runs full
      backend
- [ ] Real URL flows submit → queue → transcript → LLM → `processed`
      (observed via polling)
- [ ] Cache hit returns 200 without re-processing
- [ ] Malformed input → 400; over-limit → 429

---

## P6 — UI (M6)

**Branch:** `feature/web-client`

- [ ] Scaffold `web/` (React + TypeScript) + MUI + `react-router-dom`,
      `ThemeProvider`/`CssBaseline`
- [ ] Main page (`/`) — submit form + history list, navigates to
      `/video/{id}` on 200/202
- [ ] Detail page (`/video/:id`) — polls `GET /api/summaries/{id}` (~1.5s),
      renders per-status view (`new`/`fetching-transcript`/`summarizing` →
      spinner + stage label; `processed` → player + summary; `failed` →
      alert + resubmit)
- [ ] `VideoPlayer` component — presentational 16:9 iframe embed, `videoId`
      prop only
- [ ] History list — `GET /api/summaries`, newest first
- [ ] Surface API errors via `Alert`/`Snackbar` (400, 429, 503, async
      `failed`)
- [ ] `web/Dockerfile`; `web` uncommented in `docker-compose.yml`

**Acceptance:**
- [ ] `docker compose up` (all services) brings up the full app end-to-end
- [ ] Submit → detail page → processing → player + summary → findable in
      history
- [ ] No summarization/parsing logic in the frontend

---

## Open questions carried from spec §12

Each already has a chosen starting point in `plan.md`; revisit only if a
constraint changes.

- [x] Processing execution → async transcript worker over RabbitMQ +
      api-side summarization consumer
- [x] Status delivery → polling with stage-based progress
- [x] Transcript persistence → store summary only; `TranscriptReady` carries
      transcript inline
- [ ] Production LLM provider / Azure AI Foundry — interim: GitHub Models
- [ ] Messaging reliability — retry policy, DLQ, topology (resolved in P3c/P4)
- [ ] Rate-limit thresholds — decide concrete numbers in P5
- [ ] Stuck in-flight record timeout/reclaim — decide in P5
- [ ] Auth / multi-user — none planned; history is global
