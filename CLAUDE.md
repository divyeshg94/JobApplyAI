# CLAUDE.md

Instructions for Claude Code sessions working in this repo. Architecture below is locked from prior design discussion — don't re-derive or re-litigate it. Full detail: [docs/architecture.md](docs/architecture.md).

## What this is

Solo job-application automation tool (JobApplyAi), designed to evolve into multi-tenant SaaS later without a schema rework. Currently early scaffold stage — see `docs/architecture.md` §9 for milestone sequence.

## Locked decisions — do not re-litigate

- **.NET 10** across every project (needed for EF Core 10 native `vector` column support).
- **DB**: Azure SQL only, via EF Core `UseAzureSql(...)`. No separate vector DB / Azure AI Search.
- **Files**: Azure Blob Storage, private containers, short-lived SAS URLs only — never proxy file bytes through the API.
- **AI**: Microsoft Foundry (`Azure.AI.Projects` + `Microsoft.Extensions.AI`) for parsing, embeddings, match scoring, and tailored doc generation.
- **Hosting**: one Azure App Service running the Web API + a `BackgroundService` in-process. No Azure Functions, no Container Apps. **Always On required (Basic tier+)** — the background service dies on Free/Shared tiers.
- **Web UI**: Blazor Server, hosted inside `JobApplyAi.Api` (`Components/`). Not a separate project, not a SPA. Dashboard talks to services in-process — no HTTP/CORS/API-key for its own pages.
- **Email**: MailKit + Gmail SMTP app password.
- **Job sources v1**: Greenhouse + Lever (official public ATS job-board APIs) and Adzuna (official free-tier aggregator API) only. **Never scrape LinkedIn/Indeed directly** — no public API for job seekers exists there; that's a ToS/legal risk this project deliberately avoids.
- **No auth v1**: single hardcoded `SeedData.DefaultUserId`. Every user-scoped table still carries a `UserId` FK so real auth is additive later — never add a table without it.
- **No CLI v1.**
- **Human always clicks Submit.** The extension autofills form fields and stages the tailored file for download; it never auto-submits an application. Do not build toward silent auto-submit.
- **File-attachment field is a known, accepted manual step** (browsers block scripting `input[type=file].files`). Don't try to solve this with `chrome.debugger` tricks or other workarounds — it's a deliberate v1 limitation, documented in `extension/README.md` once that exists.
- **Resume parse always goes through human review** before a profile becomes `Active` and drives matching — never skip the review/edit step even if Foundry's parse looks clean.
- **Azure SQL is cohosted, not dedicated.** All tables live in the `jobapply` schema of an existing server (the user's separate Azure DevOps DORA-metrics DB), with a dedicated migrations-history table (`jobapply.__EFMigrationsHistory_JobApply`). Never create a table outside `jobapply`, never assume `dbo` is ours, never touch other schemas on that server. Requires host DB compatibility level 170 (native `json`/`vector`). Detail: `docs/architecture.md` Status section.

## Conventions

- **Project references**: `Domain` → no deps. `Infrastructure` → `Domain` only. `Api` → `Domain` + `Infrastructure`. Never let `Domain` reference EF Core, Azure SDKs, or anything else concrete — interfaces only.
- **Job source adapters**: one class per source implementing `IJobSourceClient` (see architecture doc §4). Adding a new source = new adapter + new enum value, never touch the polling loop/dedupe/embedding/matching code to add one.
- **Dedup key**: `(Source, ExternalJobId)`, enforced as a unique constraint — never rely on application-level checks alone. Cross-source duplicates (same job via Adzuna and Greenhouse) are a known accepted v1 limitation — mitigate at the notification layer if needed, never by merging rows.
- **Polling cadence is per-source** (`Polling:Sources:{Source}:IntervalMinutes`), not one global interval — Adzuna's free-tier quota (~250 calls/day) burns out under aggressive polling; Greenhouse/Lever are keyless and cheap.
- **Extension fetches go through the service worker only** — MV3 content-script fetches carry the host page's origin, not `chrome-extension://<id>`. Content scripts message-pass to the service worker, which holds the API client + key.
- **Secrets**: API key, SMTP creds, Adzuna `app_id`/`app_key`, Azure connection strings — local dev via `dotnet user-secrets`, never in `appsettings.json` or committed files. See `.gitignore`.
- **No-auth-v1 still needs a gate**: every API endpoint (not just extension-facing ones) sits behind the shared-secret API-key middleware once it exists (milestone 3) — the App Service URL is public from first deploy.

## Things NOT to do

- Don't add Azure Functions or Container Apps — hosting model is fixed (single App Service).
- Don't scrape job boards without an official API.
- Don't build multi-tenant auth yet — just keep `UserId` threaded through schema so it's additive later.
- Don't add a CLI project.
- Don't let the extension auto-submit an application or auto-attach a file via non-standard browser APIs.
- Don't skip the resume review/edit step in the parse flow.

## Current status

Milestones 1–5 are verified live, not just code-complete (see `docs/architecture.md` Status section): schema applied to the cohosted DB, a real resume went through upload → parse → review → activate, Greenhouse polling pulled real postings (Databricks + Twilio boards) with correct dedup, the matching pipeline (embed → classify → prefilter → LLM rescore) is producing real `MatchResult` rows, and a real digest email was received. Lever/Adzuna adapters are still code-only, untested live. Matching now includes hard filters beyond the original plan, all applied in SQL before the LLM ever sees a posting — `RequiresVisaSponsorship`, `MinimumSalaryUsd`, `RequiredCountry`, `ExcludedCompanies` (per-user, opt-in) and `ApplicationDeadline` (unconditional). `/matches` dashboard exists (status filter, Dismiss, live deadline re-check).

Milestones 6 (tailored resume/cover-letter generation) and 7 (browser extension + autofill) are code-complete but not yet live-verified — see `docs/architecture.md` Status section for the full breakdown. Two migrations landed mid-session (deadline/classification columns, work-location-country) that may not be applied to the live DB yet — re-run `docs/schema-initial.sql` (idempotent) before testing. Only milestone 8 (end-to-end test) hasn't started, and it can't really start until 6 and 7 are confirmed working — it's the same loop, just observed start to finish.

While waiting on live validation, ran a self-review pass (`/code-review`) over the M6/M7 diff and added test coverage for previously-untested logic — see `docs/architecture.md`'s "Bugs found via self-review" entry. Found and fixed 5 real issues this way, including two extension field-matching bugs (wrong-field regex, wrong-control selection on compound fields) that live testing alone might not have caught on the first try since they only misfire on specific label text. Also added `tests/.../Ai/FoundryApplicationDocumentGeneratorTests.cs` covering the resume-fabrication guard — previously zero automated coverage on the app's single highest-stakes correctness property.

## Next step

Verify milestones 6 and 7 live, in order:
1. Milestone 6: one real "Generate documents" click on `/matches` — confirm the resume PDF doesn't fabricate work history (bullets should trace back to the original profile text), cover letter reads sensibly, "Mark applied" flips status.
2. Milestone 7: load `extension/` unpacked (see `extension/README.md`), configure the popup (API base URL + key), open a real Greenhouse or Lever job page, confirm the content script fires and fills fields correctly — pay attention to the React-controlled-input handling (`content/field-mapper.js`) actually registering with the page's own form state, not just visually showing a value.

Once both check out, milestone 8 is just running the whole loop start to finish once and writing down what happened — no new code expected unless something breaks.
