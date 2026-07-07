# youtube-reader

## What this is

Takes a YouTube video URL, fetches its transcript, and produces a short LLM-generated summary.
Reference/inspiration for UX and feature scope: https://notegpt.io/youtube-video-summarizer

Flow: user pastes a YouTube link → backend fetches the transcript (YoutubeExplode) → transcript is
sent to an LLM for summarization → summary is returned and rendered in the UI.

Processed videos and their summaries are persisted, so the user can browse a history of
previously summarized links instead of re-processing them.

## Tech stack

- **Frontend**: React (TypeScript)
- **Backend**: .NET minimal API
- **Transcript extraction**: [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode)
- **Summarization**: LLM call, abstracted behind an interface (provider TBD — see "LLM hosting" below)
- **Database**: MongoDB — stores processed videos as documents (video link + processing result),
  backing the `ISummaryRepository` implementation

## Architecture

Follow **SOLID**, **Clean Architecture**, and **DDD**. Practically, that means for the .NET backend:

- `Domain` — core model for this app: `Video`, `Transcript`, `Summary` entities/value objects
  (e.g. a `VideoId`/`VideoUrl` value object that owns YouTube URL parsing/validation). No
  framework or infrastructure references here.
- `Application` — use cases (e.g. `SummarizeVideo`, `GetSummaryHistory`), expressed as interfaces
  the domain/use-case layer depends on: `ITranscriptFetcher`, `ISummarizer`, `ISummaryRepository`.
  No YoutubeExplode, LLM SDK, or database references here either — only abstractions.
- `Infrastructure` — concrete adapters: `YoutubeExplodeTranscriptFetcher : ITranscriptFetcher`,
  an `ISummarizer` implementation per LLM provider, and a MongoDB-backed `ISummaryRepository`
  implementation (one document per processed video: video link + processing result).
- `Api` — minimal API endpoints + composition root (DI wiring). Thin: maps HTTP to
  use-case calls, no business logic.

Keep the `ISummarizer`/`ITranscriptFetcher` boundary strict — the LLM provider and the transcript
source are both things we expect to swap out, so nothing above `Infrastructure` should know which
concrete provider is in use.

Mirror this layering under `tests/` per layer (unit tests for `Domain`/`Application`, integration
tests for `Infrastructure` adapters).

Frontend is a thin client against the backend's HTTP API — no business logic (summarization
rules, transcript parsing) duplicated there.

## LLM hosting — open decision

Not yet decided. No Azure AI Foundry access yet. Interim plan: implement `ISummarizer` against a
directly-hosted API (OpenAI or Anthropic) so development isn't blocked, and swap to Azure AI
Foundry later purely by adding a new `Infrastructure` adapter — the `Application` layer shouldn't
need to change. Revisit once Foundry access is available.

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
