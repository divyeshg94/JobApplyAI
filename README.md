# JobApplyAi

AI-assisted job application pipeline: finds jobs, scores fit against your resume, tailors a resume+cover letter per job, and semi-automates filling the application form. Built solo-first, designed to evolve into a multi-tenant product later without a schema rework.

## Status

Architecture locked (see [docs/architecture.md](docs/architecture.md)). Milestones 1–5 are **verified live** — a real resume has gone through the full loop, real Greenhouse postings have been matched and emailed. Milestones 6–7 are built and merged but not yet exercised by the user live. Full per-milestone breakdown, including bugs found and fixed along the way: [docs/architecture.md § Status](docs/architecture.md#status).

| # | Milestone | State |
|---|---|---|
| 0 | Azure provisioning | Partial — Foundry, Blob, and SQL (cohosted) live; Gmail SMTP live; App Service and Adzuna credentials still pending |
| 1 | Solution scaffold + DB schema | **Verified live** |
| 2 | Resume upload → parse → review → save | **Verified live** |
| 3 | Job source adapters + polling + security | **Verified live** (Greenhouse only — Lever/Adzuna are code-complete, untested live) |
| 4 | Matching (embed → classify → prefilter → LLM rescore) | **Verified live** |
| 5 | Notifications (digest email + dashboard) | **Verified live** |
| 6 | Tailored resume/cover-letter generation | Code complete, merged — needs one real "Generate documents" click |
| 7 | Browser extension + autofill | Code complete, merged, self-reviewed — needs a real Chrome load on a real job page |
| 8 | End-to-end test | Not started — blocked on 6 and 7 being confirmed live |

## What it does

1. Upload your resume → Microsoft Foundry parses it into a structured profile → you review/edit before it goes live.
2. A background service polls Greenhouse, Lever (official ATS job-board APIs), and Adzuna (aggregator, free tier) on a timer.
3. New postings are deduped, embedded, and vector-prefiltered (Azure SQL native vector search) against your profile.
4. Top candidates get rescored by an LLM (Foundry) for a fit score + reasoning.
5. Good matches trigger an email (Gmail SMTP) + in-app notification.
6. On demand, Foundry generates a tailored resume + cover letter (stored in Azure Blob Storage).
7. A Chrome extension (Manifest V3) autofills the application form on Greenhouse/Lever pages by calling the API directly. You manually attach the pre-downloaded tailored file (browsers block scripted file-input assignment) and click Submit yourself — nothing submits without you.

## Repo layout

```
JobApplyAi.sln
src/
  JobApplyAi.Domain/          entities + interfaces, no dependencies
  JobApplyAi.Infrastructure/  EF Core, job-source adapters, Foundry/Blob/email clients
  JobApplyAi.Api/             ASP.NET Core Web API + Blazor Server UI + background polling service
tests/
  JobApplyAi.Domain.Tests/
  JobApplyAi.Infrastructure.Tests/
  JobApplyAi.Api.Tests/
extension/                    Chrome extension (Manifest V3, plain JS, no build step) — not a .NET project
docs/
  architecture.md             full design reference
  schema-initial.sql          idempotent DB schema script — re-run after pulling new migrations
```

## Prerequisites

- .NET 10 SDK
- Azure SQL Database, compatibility level 170+ (native `vector`/`json` column support) — ✅ live (cohosted, see architecture doc)
- Azure Blob Storage account — ✅ live
- Microsoft Foundry project (chat + embedding model deployments) — ✅ live
- Gmail account with 2-Step Verification enabled + an [app password](https://myaccount.google.com/apppasswords) for SMTP — ✅ live
- Azure App Service plan, **Basic tier or higher** — Always On is required or the background polling service silently stops when the idle app unloads (Free/Shared tiers can't enable it) — not yet provisioned (running via `dotnet run` locally so far)
- Adzuna API `app_id`/`app_key` (free tier — [developer.adzuna.com](https://developer.adzuna.com/); quota-limited, polled a few times a day, not continuously) — not yet obtained
- Google Chrome, for the extension — see [extension/README.md](extension/README.md) to load it unpacked

## Running locally

```bash
# connection string via user-secrets — never in appsettings.json
dotnet user-secrets set "ConnectionStrings:AzureSql" "<azure-sql-connection-string>" --project src/JobApplyAi.Api

# apply schema (or run docs/schema-initial.sql manually against the DB)
JOBAPPLYAI_SQL="<azure-sql-connection-string>" dotnet ef database update --project src/JobApplyAi.Infrastructure --startup-project src/JobApplyAi.Api

dotnet run --project src/JobApplyAi.Api
```

To load the extension: see [extension/README.md](extension/README.md) (unpacked load in Chrome, `chrome://extensions`, Developer mode).

## Docs

- [docs/architecture.md](docs/architecture.md) — full architecture, schema, adapter design, API contract, milestone sequence
- [CLAUDE.md](CLAUDE.md) — conventions and guardrails for AI-assisted work in this repo
