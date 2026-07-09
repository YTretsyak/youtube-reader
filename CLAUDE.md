# youtube-reader

## What this is

Takes a YouTube video URL, fetches its transcript, and produces a short LLM-generated summary.
Reference/inspiration for UX and feature scope: https://notegpt.io/youtube-video-summarizer

Flow: user pastes a YouTube link → the `api` publishes a transcript request over RabbitMQ → a
standalone `transcript-service` fetches the transcript (YoutubeExplode) and publishes it back →
`api` sends the transcript to an LLM for summarization → summary is persisted, and the UI polls
until it's ready and renders it.

Processed videos and their summaries are persisted, so the user can browse a history of
previously summarized links instead of re-processing them.

## Tech stack

- **Frontend**: React (TypeScript) with **Material UI (MUI)** as the component
  library — `@mui/material` with the Emotion styling engine
  (`@emotion/react`, `@emotion/styled`)
- **Backend**: .NET minimal API (`api`) + a standalone .NET worker (`transcript-service`)
- **Transcript extraction**: [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) — an
  in-process .NET library (no API key) hosted **inside the standalone `transcript-service`**, so
  the fragile, rate-limited fetch layer can be scaled independently. `api` never references
  YoutubeExplode; it reaches the service only over RabbitMQ.
- **Messaging**: RabbitMQ — the broker between `api` and `transcript-service`. Communication is
  asynchronous (event-driven), never direct HTTP. Hosted as its own container.
- **Summarization**: LLM call, abstracted behind an interface (provider TBD — see "LLM hosting" below)
- **Database**: MongoDB — stores processed videos as documents (video link + processing result),
  backing the `ISummaryRepository` implementation

## Architecture

Follow **SOLID**, **Clean Architecture**, and **DDD**. Practically, that means for the .NET backend:

- `Domain` — core model for this app: `Video`, `Transcript`, `Summary` entities/value objects
  (e.g. a `VideoId`/`VideoUrl` value object that owns YouTube URL parsing/validation). No
  framework or infrastructure references here.
- `Application` — use cases (e.g. `SummarizeVideo`, `GetSummaryHistory`, plus a handler reacting to
  transcript results), expressed against interfaces only: `ITranscriptRequestPublisher`,
  `ISummarizer`, `ISummaryRepository`. No YoutubeExplode, LLM SDK, database, or RabbitMQ client
  references here — only abstractions.
- `Contracts` — the integration-event schemas shared between `api` and `transcript-service`
  (`TranscriptRequested`, `TranscriptReady`, `TranscriptUnavailable`). No logic.
- `Infrastructure` — concrete adapters: `RabbitMqTranscriptRequestPublisher :
  ITranscriptRequestPublisher`, an `ISummarizer` implementation per LLM provider, a MongoDB-backed
  `ISummaryRepository`, and the RabbitMQ consumer plumbing.
- `Api` — minimal API endpoints + composition root (DI wiring) + the consumer that reacts to
  transcript results. Thin: maps HTTP to use-case calls, no business logic.
- `TranscriptService` — a **standalone .NET worker**, deployed separately and independently
  scalable. It consumes `TranscriptRequested`, runs YoutubeExplode via
  `YoutubeExplodeTranscriptFetcher : ITranscriptFetcher` (this boundary lives **here**, not in
  `api`), and publishes `TranscriptReady`/`TranscriptUnavailable`.

Keep the `ISummarizer`/`ITranscriptFetcher` boundary strict — the LLM provider and the transcript
source are both things we expect to swap out, so nothing above `Infrastructure` should know which
concrete provider is in use. `api` reaches transcript fetching only through
`ITranscriptRequestPublisher` + RabbitMQ, never YoutubeExplode directly.

YoutubeExplode works by scraping reverse-engineered YouTube endpoints, so it's inherently fragile
and can be rate-limited — isolating it in its own service is exactly why the messaging boundary
matters (it can be scaled out and its failures contained). Treat a video with no captions as a
normal domain outcome (published as `TranscriptUnavailable` → a failed result), not an exception,
and reuse a single `HttpClient` (via `IHttpClientFactory`) across requests. Message handling is
at-least-once with **manual acknowledgment** — a consumer acks only *after* publishing its result,
so a worker that crashes mid-process leaves the message unacked and the broker redelivers it;
handlers are therefore idempotent, with retry-cap + dead-lettering for poison messages. See
`docs/specification.md` for the adapter and messaging details.

Mirror this layering under `tests/` per layer. **Unit tests only for now** — `Infrastructure`
adapters and the `TranscriptService` worker are tested against mocks/fakes (e.g. a mocked
`HttpMessageHandler`, an in-memory broker/repo double), not real Mongo/RabbitMQ/GitHub Models.
Integration tests against real services are deferred; revisit once the adapters are stable.

Frontend is a thin client against the backend's HTTP API — no business logic (summarization
rules, transcript parsing) duplicated there.

## LLM hosting

No Azure AI Foundry access yet; production provider still open. **Interim: GitHub Models** (free
prototyping tier; auth is a GitHub PAT with `models: read`, not a Claude/ChatGPT subscription — a
consumer chat subscription can't be used programmatically). It's an **OpenAI-compatible** Chat
Completions endpoint, so `ISummarizer` is one `HttpClient`-based adapter configured by
`LLM_BASE_URL`/`LLM_API_KEY`/`LLM_MODEL` — repointing at Ollama (local, free), Groq, or OpenAI is a
config change, not a code change. GitHub Models is rate-limited and prototyping-only. Swap to Azure
AI Foundry later purely by adding a new `Infrastructure` adapter — the `Application` layer shouldn't
need to change. See `docs/specification.md` §7 for the adapter details.

**Nothing is self-hosted** — GitHub runs the inference; the PAT is just the HTTP credential. The
endpoint is **verified working** (smoke-tested against `openai/gpt-4o-mini`). To re-check the token,
run the smoke test in `docs/specification.md` §7 ("Setting up GitHub Models") — it reads
`LLM_API_KEY` from the git-ignored `.env` and hits `POST {LLM_BASE_URL}/chat/completions`.

## Git workflow — GitFlow

This project uses GitFlow for branching.

- `main` — official release history only. Every commit here is a released version, tagged
  (e.g. `v1.0`).
- `develop` — integration branch. All finished feature work lands here first.
- `feature/*` — one branch per feature, created off `develop`, merged back into `develop` when
  done.
- `release/*` — cut from `develop` once it has enough features for a release; only bugfixes,
  docs, and release-chores happen here. Merges into both `main` (tagged) and back into `develop`.
- `hotfix/*` — cut from `main` for urgent production fixes. Merges into both `main` (tagged) and
  `develop`.
- `bugfix/*` — fixes targeting `develop` (non-urgent, not yet released).

Branch naming: `feature/<short-description>`, `release/<version>`, `hotfix/<short-description>`,
`bugfix/<short-description>`.

## Conventions

- No code comments explaining *what* code does — only *why*, when non-obvious.
- Don't add abstractions/config beyond what's needed for the current use case.
