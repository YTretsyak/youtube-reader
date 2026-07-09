# Video detail page with player + summary — Design

Adds an in-app detail view so a user can watch the submitted video alongside
its summary, instead of only reading text. Extends the Phase 6 (M6, web
client) scope in [`plan.md`](../../plan.md); no domain, application, or
messaging changes.

## Goal

On submit, the user is taken to a page dedicated to that video. It shows a
spinner while the video is processing, then reveals a YouTube player and the
summary together once processing succeeds — or an error if it fails.

## Routing

Introduce **React Router** (`react-router-dom`), not yet listed in the plan's
Phase 6 dependencies. Two routes:

- `/` — main page: submit form (MUI `TextField` + `Button`) + history list
  (`GET /api/summaries`).
- `/video/:id` — detail page: owns polling and the state-to-view mapping
  below.

`POST /api/summaries` returns a record `id` for every outcome (`200` cache
hit, `202` new/in-flight). The submit form navigates to `/video/{id}` in both
cases — the detail page renders correctly from whichever status it first
polls, so cache-hit and freshly-submitted flows converge on one view. History
list items link to the same route. A direct load of `/video/:id` (refresh,
shared link, back button) works the same way: fetch once, then poll if still
in-flight.

## Video player

A presentational `VideoPlayer` component: a responsive 16:9 wrapper around a
plain `<iframe src="https://www.youtube.com/embed/{videoId}">`. No player
library (e.g. `react-youtube`) — a bare iframe is sufficient and keeps to the
"no abstractions beyond current need" convention. Takes `videoId` as its only
prop; no knowledge of summaries, polling, or status.

## Detail-page state → view mapping

The detail page polls `GET /api/summaries/:id` (~1.5 s, matching spec §6)
and renders exactly one of three views per `status`:

| status | view |
| --- | --- |
| `new` / `fetching-transcript` / `summarizing` | Full-page spinner (MUI `CircularProgress`) + stage label ("Queued…", "Fetching transcript…", "Summarizing…"). |
| `processed` | `VideoPlayer` + summary `Card` (title, summary text) shown together. Stop polling. |
| `failed` | MUI `Alert` with `error`. No player. Offer resubmit (re-`POST`s the same URL). Stop polling. |

The player and summary appear together only on `processed` — not
progressively — matching the "spinner first, then player + summary together"
choice over showing the player immediately at submit time.

## Backend contract change

`GET /api/summaries/{id}` must include `videoId` in its response so the
frontend can build the embed URL without re-deriving it. The field already
exists on the stored document (spec §6) and is already returned by
`GET /api/summaries` (history); this adds it to the single-record response
too. No new logic — a response-shape addition to Phase 5.

## Out of scope

- No new persistence, domain, or messaging changes.
- No player library / custom player chrome beyond the standard YouTube embed
  iframe.
- No progressive reveal (player before summary, or vice versa) — both appear
  together on `processed`.
- No changes to the `failed` → resubmit flow beyond what spec §6 already
  describes.

## Affected docs

- `docs/specification.md` — §5 (`GET /api/summaries/{id}` response shape),
  §6 (progress-delivery UX), frontend view list.
- `docs/plan.md` — Phase 6 tasks: add router dependency, `/video/:id` page,
  `VideoPlayer` component, the three-view state mapping.
