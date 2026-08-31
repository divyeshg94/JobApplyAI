# JobApplyAi

AI-assisted job application pipeline: finds jobs, scores fit against your resume, tailors a resume+cover letter per job, and semi-automates filling the application form. Built solo-first, designed to evolve into a multi-tenant product later without a schema rework.

## Status

Architecture locked (see [docs/architecture.md](docs/architecture.md)). Milestones 1–3 are code-complete; live-DB verification and a real end-to-end resume run are the open items. Full per-milestone breakdown: [docs/architecture.md § Status](docs/architecture.md#status).

| # | Milestone | State |
|---|---|---|
| 0 | Azure provisioning | Partial — Foundry + Blob already provisioned; SQL cohosted in an existing server (see architecture doc); App Service/Gmail/Adzuna creds pending |
| 1 | Solution scaffold + DB schema | Code complete — live-DB apply not yet confirmed |
| 2 | Resume upload → parse → review → save | Code complete — untested end-to-end |
| 3 | Job source adapters + polling + security | Code complete |
| 4–8 | Matching, notifications, doc generation, extension, e2e | Not started |

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
extension/                    Chrome extension (Manifest V3, plain TS) — not a .NET project
docs/
  architecture.md             full design reference
```

`src/` and `tests/` are scaffolded (milestone 1). `extension/` lands with milestone 7.

## Prerequisites

- .NET 10 SDK
- Azure SQL Database, compatibility level 170+ (native `vector`/`json` column support) — ✅ have (cohosted, see architecture doc)
- Azure Blob Storage account — ✅ have
- Microsoft Foundry project (chat + embedding model deployments) — ✅ have
- Azure App Service plan, **Basic tier or higher** — Always On is required or the background polling service silently stops when the idle app unloads (Free/Shared tiers can't enable it) — not yet provisioned
- Gmail account with 2-Step Verification enabled + an [app password](https://myaccount.google.com/apppasswords) for SMTP (app passwords require 2FA) — not yet set up
- Adzuna API `app_id`/`app_key` (free tier — [developer.adzuna.com](https://developer.adzuna.com/); quota-limited, polled a few times a day, not continuously) — not yet obtained
- Google Chrome, for the extension

## Running locally

```bash
# connection string via user-secrets — never in appsettings.json
dotnet user-secrets set "ConnectionStrings:AzureSql" "<azure-sql-connection-string>" --project src/JobApplyAi.Api

# apply schema (or run docs/schema-initial.sql manually against the DB)
JOBAPPLYAI_SQL="<azure-sql-connection-string>" dotnet ef database update --project src/JobApplyAi.Infrastructure --startup-project src/JobApplyAi.Api

dotnet run --project src/JobApplyAi.Api
```

Extension loading steps land with milestone 7.

## Docs

- [docs/architecture.md](docs/architecture.md) — full architecture, schema, adapter design, API contract, milestone sequence
- [CLAUDE.md](CLAUDE.md) — conventions and guardrails for AI-assisted work in this repo
