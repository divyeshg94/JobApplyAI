# JobApplyAi — Architecture

Grounding facts: EF Core 10 has native `vector` type support (`SqlVector<float>`, `EF.Functions.VectorDistance()`) but requires .NET 10 (EF Core 8/9 target .NET 8 and lack it). Solution targets **.NET 10** across every project — decided over .NET 8 LTS to get native vector support without an unofficial community package. Microsoft Foundry access is via `Azure.AI.Projects` + `Microsoft.Extensions.AI` (`IChatClient` / `IEmbeddingGenerator<string, Embedding<float>>`), the same pattern EF Core's own vector-search docs use to populate `SqlVector<float>` columns.

## Status

| # | Milestone | State | Notes |
|---|---|---|---|
| 0 | Azure provisioning | Partial | Foundry + Blob Storage already provisioned (existing resources). Azure SQL: **cohosted**, not a dedicated DB — see "Database cohosting" below. App Service, Gmail app password, Adzuna `app_id`/`app_key` not yet provisioned. |
| 1 | Solution scaffold + DB schema + migrations | **Verified live** | 6-project solution, full entity model + EF configs, `InitialCreate` migration (targets `jobapply` schema), idempotent script at `docs/schema-initial.sql`. Live apply confirmed against the cohosted Velo DB (compat 170 bump + BOM-stripped script resolved the earlier errors) — `vector`/`json` columns exist and are in active use. |
| 2 | Resume upload → parse → review → save | **Verified live** | Text extraction (PdfPig/OpenXml) → `FoundryResumeParser` (JSON-mode chat call via the v1 OpenAI-compatible endpoint) → channel-based async parse worker → `/api/profile/*` endpoints → Blazor `/profile` review/edit screen. Full flow run end-to-end with a real resume: upload → parse → edit → Save & activate (embedding generated, profile `Active`). Two real bugs found and fixed along the way — see "Bugs found during live verification" below. |
| 3 | Job source adapters + polling + dedupe + security hardening | **Verified live** | `IJobSourceClient` + Greenhouse/Lever/Adzuna adapters, `JobPollingBackgroundService`, `JobSourceSubscription` CRUD, `ApiKeyMiddleware` + CORS + rate limiter. Live-tested against two real Greenhouse boards (Databricks + Twilio): 954 postings fetched (789 + 165), 0 failures, `JobsFetched == JobsNew` on first run confirming dedup logic is sound. Lever and Adzuna adapters still untested against live APIs — only Greenhouse has a real subscription so far. |
| 4 | Embeddings + vector prefilter + LLM rescore | **Verified live** | `MatchingPipelineService` (Api/Services): batched embedding (16/call), batched posting classification (one LLM call extracting visa sponsorship, salary, application deadline, and work-location country together), vector prefilter (cosine, top 25), LLM rescore via `IMatchScorer`/`FoundryMatchScorer`. Hard filters added beyond the original plan — all applied in SQL *before* the LLM ever sees a posting, never as soft prompt hints: `CandidateProfile.RequiresVisaSponsorship`, `MinimumSalaryUsd`, `RequiredCountry`, `ExcludedCompanies` (per-user preferences, opt-in) plus `ApplicationDeadline` (unconditional — a passed deadline is dead for anyone). Unstated/ambiguous values never exclude, only an explicit disqualifying value does. `JobPosting.ClassifiedAtUtc` gates what needs (re)classifying — deliberately separate from the nullable business fields, since null is also *their* valid final state (e.g. "no deadline stated"); using one of them as the gate would silently skip backfilling a posting when a new field gets added later. Live-verified: real matches with sane scores, all four hard filters confirmed via real postings that needed excluding. |
| 5 | Notifications | **Verified live** | `NotificationService`: batches all `PendingReview` matches ≥ `Matching:NotifyThreshold` (default 70) into one digest email (not one-per-match — 225+ existing matches would've been a spam storm), sent via `MailKitEmailNotifier` (Gmail SMTP/STARTTLS, app password). Notify address bootstraps from the resume's own parsed email on activation (only while `Users.Email` is still the seed placeholder — never overwrites a deliberately-set address). `/matches` dashboard: status filter, score-sorted, Dismiss action, live deadline re-check (hides matches whose posting deadline has since passed, regardless of when the match was created). Real digest email received and confirmed. |
| 6 | Tailored resume/cover-letter generation | Code complete, untested live | `IApplicationDocumentGenerator`/`FoundryApplicationDocumentGenerator`: tailors resume (rephrase/reorder only — same companies/titles/dates as the source profile, guarded at the code boundary by checking the model's returned company+title pairs exactly match the input before trusting its bullets; falls back to the untailored original text if they don't) + writes a fresh cover letter. `QuestPdfDocumentRenderer` lays out both as PDF (pure layout, no AI — kept separate from generation so either can change independently). `ApplicationGenerationService` orchestrates: generate → render → upload to Blob (`generated/{userId}/{applicationId}/{resume,cover-letter}.pdf`) → upsert `Application` (`Status = Prepped`). `POST /api/applications/{matchResultId}/generate|mark-applied`, `GET /api/applications/{matchResultId}` (used by the extension, M7); `/matches` dashboard calls the service directly in-process instead (same pattern as `ProfileActivationService`) via a fresh DI scope per click — **not** a circuit-lifetime `@inject`, which would pin one `AppDbContext` for the whole Blazor session. Route keys off `matchResultId`, not `jobPostingId` as the original sketch had it — `Application.MatchResultId` is what the schema actually carries. Blocked on: one real "Generate documents" click end-to-end. |
| 7 | Browser extension + autofill | Code complete, untested live | `extension/`: Manifest V3, plain JS, no build step (deviates from the original TS+vite sketch — simpler to iterate on, zero tooling to load unpacked). New endpoint `GET /api/applications/by-external-job/{source}/{externalJobId}` fills the gap M6 didn't cover — a content script only knows the ATS's own job ID from the URL, not a `matchResultId`; it returns profile+tailored-doc data even for postings the matching pipeline never scored, so autofill still works on any Greenhouse/Lever page. `background/service-worker.js` holds the API key and relays all fetches (content-script fetches carry the *host page's* origin, not the extension's — see §7 below and CLAUDE.md). `content/field-mapper.js` fills React-controlled inputs via the native-setter + dispatch-event pattern, label-text matching as the primary field-finding strategy (more robust across per-company form customization than hardcoded selectors). Never auto-submits, never auto-attaches the resume file (browser limitation, not a bug — banner tells the user to attach manually). Self-reviewed post-build (`/code-review`) — two real field-matching bugs found and fixed (label regex false positives, wrong-control selection on compound fields), see "Bugs found via self-review" below. Still untested against a live Chrome load — needs the user's real browser and a real job page; not something `dotnet test` can verify. |
| 8 | End-to-end test | Not started | |

**Bugs found during live verification (all fixed)**:
- `Azure.AI.OpenAI`'s `AzureOpenAIClient` only speaks old dated `api-version`s; newer Foundry resources default to the v1 API and reject them outright ("API version not supported"). Fixed by switching to the plain `OpenAI` client pointed at `<resource-root>/openai/v1/`.
- The v1 embeddings route lives at the **resource root**, not under a Foundry project path (`/api/projects/<name>/...`) — chat completions happen to work through a project-scoped URL, embeddings 404 there. Fixed by always rebuilding the v1 URL from the endpoint's authority (scheme+host), discarding whatever path was configured.
- `ResumeParsingWorker`'s `SaveChangesAsync` sat outside its try/catch — any save failure crashed the whole background worker permanently (no more resumes would ever parse until restart). Fixed: save failures now log and the loop continues.
- New child entities (`ProfileWorkExperience`/`Educations`/`Skills`) have client-set Guid keys and were only ever reached via navigation-property assignment, never an explicit `Add()`. EF's graph-fixup heuristic inferred `Modified` instead of `Added` for them, producing `UPDATE`s that matched 0 rows (`DbUpdateConcurrencyException`) since the rows never existed. Fixed in all three write paths (`ResumeParsingWorker`, `ProfileEndpoints.UpdateProfileAsync`, `Profile.razor`'s `SaveAsync`) by explicitly `db.<Set>.AddRange()`-ing new children instead of relying on fixup to infer their state.
- Azure OpenAI's `json_object` response format requires the **top-level** response to be a JSON object — a bare array at the root fails deserialization (`FoundryJobPostingClassifier` asked for `[...]`; `FoundryResumeParser`/`FoundryMatchScorer` already returned `{...}` and were unaffected). Fixed by wrapping the array in `{"classifications": [...]}`.
- `MatchingPipelineService.RunAsync` had no UI feedback path on success — `Profile.razor`'s "Save & activate" button silently succeeded (profile activated, matching pipeline started producing real `MatchResult` rows) with zero visible confirmation, reading as "nothing happened." Fixed: added an explicit activation confirmation banner.
- EF Core can't translate `.Select(x => new SomeRecord(...)).OrderByDescending(r => r.Prop)` — it composes the two lambdas into one (`x => new SomeRecord(...).Prop`) before translation, and pushing a whole record construction into an `ORDER BY` isn't supported (crashed `/matches` outright). Anonymous-type projections (`new { ... }`, used throughout `MatchingPipelineService`) don't hit this — only named `record`/`class` projections do. Fixed by ordering on the raw entity property *before* the final `Select` into the named record, not after.

**Bugs found via self-review (`/code-review`) + new test coverage, not live testing** — the user wasn't validating this session, so this was the highest-value use of the time instead of idling:
- `extension/content/*-autofill.js`: unanchored `/location|city/i` label-matching regex matched "Ethnicity" (contains "city") and "Relocation assistance needed?" (contains "location") as substrings, silently writing the candidate's home address into the wrong form field. Fixed with word-boundary matching (`/\b(location|city)\b/i`) — verified both false positives are gone and real "Location"/"City" labels still match.
- `field-mapper.js`: `label.querySelector('input, textarea, select')` returns the *first* nested control in document order — a compound field like `<label>Phone<select country-code><input number></label>` would get the phone number written into the country-code dropdown. Fixed to prefer `input` → `textarea` → `select`, since every field this extension fills is a text value, never legitimately a `<select>`.
- `ApplicationEndpoints.GetByExternalJobAsync` eagerly `.Include()`d and serialized `WorkExperiences`/`Educations` on every single page-load lookup, but no content script reads either field. Removed — trimmed `ExtensionProfileDto` to only what's actually consumed (contact fields), and updated the §7 contract doc to match reality instead of the original speculative sketch.
- `FoundryJobPostingClassifier`'s own fail-open contract (documented in its code comment) only covered a *parseable-but-wrong-shape* response (null or count mismatch) — a response that doesn't parse into the expected object at all (e.g. a bare array, the exact shape of the earlier JSON-mode bug above) threw `JsonException` straight out of the method instead of degrading gracefully. Caught while writing a regression test for that earlier bug, not by inspection. Fixed with a `try/catch (JsonException)` around the deserialize call, folded into the same fail-open path.
- Added `tests/JobApplyAi.Infrastructure.Tests/Ai/FoundryApplicationDocumentGeneratorTests.cs` — the resume-fabrication guard (§4/M6: the model's returned work experiences must exactly match the source profile's companies/titles or the code falls back to the original text) had zero automated coverage despite being the single highest-stakes correctness property in the app. Now covered: exact-match passthrough, employer drift → fallback, dropped-experience → fallback, and the skills-filtering/fallback logic. Extracted the shared `FakeChatClient` test double (was duplicated privately in `FoundryResumeParserTests`) since three test classes now need it.
**Database cohosting (deviates from the original milestone-0 plan)**: rather than a dedicated Azure SQL Database, JobApplyAi's tables live in the **`jobapply` schema** of an existing Azure SQL server — the user's Azure DevOps DORA-metrics database ("Velo") — for cost savings (avoids a second billable database). Isolation: `AppDbContext.Schema = "jobapply"` (`HasDefaultSchema`), a dedicated migrations-history table (`jobapply.__EFMigrationsHistory_JobApply`, not `dbo.__EFMigrationsHistory`), no table-name overlap with the DORA schema. Requires the host DB at **compatibility level 170** for native `json`/`vector` support — that was bumped on the shared DB, a shared-blast-radius tradeoff worth remembering if the DORA tooling ever misbehaves. Clean exit path if this ever needs to split out: `DROP SCHEMA jobapply` (after dropping its tables) — nothing here touches `dbo`.

## 1. Solution / repo layout

```
Ingenious.JobApplyAi/
├── JobApplyAi.sln
├── Directory.Build.props        # net10.0, Nullable, ImplicitUsings, analyzers
├── global.json                  # pin SDK to 10.0.x
├── .editorconfig
├── .gitignore
├── README.md
├── src/
│   ├── JobApplyAi.Domain/
│   │   JobApplyAi.Domain.csproj             # no project/3rd-party deps — POCOs + interfaces only
│   ├── JobApplyAi.Infrastructure/
│   │   JobApplyAi.Infrastructure.csproj     # → Domain
│   └── JobApplyAi.Api/
│       JobApplyAi.Api.csproj                # → Domain, Infrastructure
│       Components/                          # Blazor Server UI (review screen, match dashboard)
├── tests/
│   ├── JobApplyAi.Domain.Tests/
│   ├── JobApplyAi.Infrastructure.Tests/     # EF Core model + adapter tests (WireMock)
│   └── JobApplyAi.Api.Tests/                # WebApplicationFactory integration tests
├── extension/                    # NOT a .NET project — plain JS/TS + manifest.json
│   ├── manifest.json              # Manifest V3
│   ├── src/
│   │   ├── background/service-worker.ts
│   │   ├── content/greenhouse-autofill.ts
│   │   ├── content/lever-autofill.ts
│   │   ├── content/field-mapper.ts           # shared field-mapping logic
│   │   ├── popup/                            # status, config, manual trigger
│   │   └── lib/api-client.ts                 # typed wrapper for the REST contract (§7)
│   ├── icons/
│   ├── package.json / tsconfig.json / vite.config.ts
│   └── README.md
└── docs/
    └── architecture.md           # this file
```

**Why 3 .NET projects, not 4**: orchestration/use-case services (`ResumeParsingService`, `MatchingPipelineService`, `ApplicationGenerationService`) live in `JobApplyAi.Api/Services/` rather than a separate `Application` project — avoids an extra layer for a solo build. Natural extraction point if/when this becomes a real product; nothing here blocks that later.

**Web UI = Blazor Server, hosted inside `JobApplyAi.Api`** (`Components/` folder, framework-provided — no extra package). Same process/App Service as the API and background service: dashboard calls services in-process (no HTTP, no CORS, no API key needed for its own pages), one deployable. Covers the resume review/edit screen (milestone 2) and the match dashboard (milestone 5). If the SaaS pivot later demands a separate SPA, the API contract already exists for the extension — the Blazor UI extracts or gets replaced without touching the API surface.

**Project reference rules**:
- `Domain` → no project references. Entities, enums, interfaces Infrastructure implements (`IJobSourceClient`, `IBlobStorageService`, `IResumeParser`, `IJobEmbeddingService`, `IMatchScorer`, `IEmailNotifier`, repo interfaces if used).
- `Infrastructure` → references `Domain` only. `AppDbContext`, EF Core configs/migrations, the 3 job-source adapters, Foundry client wrappers, Blob wrapper, MailKit sender, resilience policy setup.
- `Api` → references `Domain` + `Infrastructure`. Composition root (`Program.cs`), endpoints, `JobPollingBackgroundService`, DI wiring, CORS, API-key middleware, appsettings.

## 2. NuGet packages per project

**`JobApplyAi.Domain`** — none beyond BCL. Keep pure/testable.

**`JobApplyAi.Infrastructure`**

| Concern | Package |
|---|---|
| EF Core | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.SqlServer` (10.x, `UseAzureSql(...)` for native `json`/`vector`), `Microsoft.EntityFrameworkCore.Design` |
| Blob storage | `Azure.Storage.Blobs` |
| Foundry / AI | `Azure.AI.Projects`, `Azure.AI.Extensions.OpenAI`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI` — **verify exact package names at milestone 2**; the Foundry .NET SDK naming has churned recently |
| PDF rendering | `QuestPDF` (community license, free under $1M revenue) — renders tailored resume/cover-letter to PDF; LLM outputs structured text, not PDFs |
| Azure auth | `Azure.Identity` (Managed Identity in App Service for Blob + Foundry) |
| Email | `MailKit` (Gmail SMTP + app password) |
| Resilience/retry | `Microsoft.Extensions.Http.Resilience` (Polly v8 under the hood, `AddStandardResilienceHandler()`) |
| HTTP typed clients | `Microsoft.Extensions.Http` |
| Docx text extraction (optional pre-pass) | `DocumentFormat.OpenXml` — only if Foundry can't parse raw file bytes directly; decide at milestone 2 |

**`JobApplyAi.Api`**

| Concern | Package |
|---|---|
| Web framework | ASP.NET Core (SDK-provided) |
| Web UI | Blazor Server (SDK-provided — components live in `Api/Components/`) |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` (+ Swashbuckle if needed) |
| Rate limiting | `Microsoft.AspNetCore.RateLimiting` (built-in) |
| CORS | built-in `Microsoft.AspNetCore.Cors` |
| Local secrets | `Microsoft.Extensions.Configuration.UserSecrets` |

**`tests/*`**: `xunit`, `Microsoft.NET.Test.Sdk`, `Microsoft.AspNetCore.Mvc.Testing` (Api.Tests), `WireMock.Net` (Infrastructure.Tests — mock Greenhouse/Lever/Adzuna so adapter tests never hit real APIs), EF tests against a real SQL Server 2025/Azure SQL instance (InMemory provider can't exercise `vector`/`json` mapping).

## 3. Domain entities / schema

Every user-scoped table carries `UserId` (Guid FK → `Users.Id`), hardcoded to `SeedData.DefaultUserId` (a `static readonly Guid` in `Domain/Seed/SeedData.cs`), seeded via EF `HasData()`. No auth middleware reads it yet — Api layer code uses the constant directly — but the column/FK exists everywhere so real auth later is additive (swap the constant for a claims lookup), not a migration.

```
Users
 ├─ Id (PK, uniqueidentifier), Email, DisplayName, CreatedAtUtc

CandidateProfiles                       -- one row per resume upload/version; one "Active" drives matching
 ├─ Id (PK), UserId (FK)
 ├─ Status (enum: Parsing | NeedsReview | Active | Superseded | Failed)
 ├─ RawResumeBlobUrl, RawResumeFileName, RawResumeContentType
 ├─ FullName, Email, Phone, LocationText, LinkedInUrl, PortfolioUrl
 ├─ SummaryText
 ├─ ProfileEmbedding (SqlVector<float>(1536), nullable until confirmed)
 ├─ RequiresVisaSponsorship (bool), MinimumSalaryUsd (int, nullable — no floor set),
 │  RequiredCountry (ISO 3166-1 alpha-2, nullable — no restriction)  -- hard matching filters, added at M4
 ├─ CreatedAtUtc, ParsedAtUtc, ReviewedAtUtc

ProfileWorkExperiences
 ├─ Id (PK), CandidateProfileId (FK)
 ├─ Company, Title, LocationText, StartDate, EndDate (nullable), IsCurrent
 └─ DescriptionText (free text; no separate bullets table for v1)

ProfileEducations
 ├─ Id (PK), CandidateProfileId (FK)
 ├─ Institution, Degree, FieldOfStudy, StartDate, EndDate

ProfileSkills
 ├─ Id (PK), CandidateProfileId (FK)
 ├─ Name, Category (nullable: "Language" | "Framework" | "Tool")

ProfileExcludedCompanies                -- hard matching filter, added at M4
 ├─ Id (PK), CandidateProfileId (FK)
 ├─ CompanyName

JobSourceSubscriptions                  -- what the poller watches
 ├─ Id (PK), UserId (FK)
 ├─ Source (enum: Greenhouse | Lever | Adzuna)
 ├─ ConfigJson (json: {"boardToken":"acme"} GH, {"company":"acme"} Lever,
 │              {"keywords":"...","location":"...","country":"us"} Adzuna)
 ├─ DisplayName, IsEnabled
 ├─ LastPolledAtUtc, LastPollStatus (enum: Ok | Error), LastPollError (nullable)

JobPostings
 ├─ Id (PK), Source (enum), ExternalJobId
 ├─ UNIQUE (Source, ExternalJobId)        -- dedup key
 ├─ Title, CompanyName, LocationText, DescriptionText, ApplyUrl
 ├─ PostedAtUtc (nullable), FetchedAtUtc
 ├─ RawJsonPayload (json — full source payload, kept for debugging/reprocessing)
 ├─ JobEmbedding (SqlVector<float>(1536), nullable until embedded)
 ├─ IsActive (bool — future delisting detection)
 ├─ ClassifiedAtUtc (nullable) -- gates (re)classification; separate from the fields below since
 │  null is also THEIR valid final state, e.g. "no deadline stated"
 ├─ VisaSponsorship (enum: Sponsors | NoSponsorship | Unspecified, nullable)
 ├─ SalaryMinAnnualUsd, SalaryMaxAnnualUsd (int, nullable)
 ├─ ApplicationDeadline (date, nullable), WorkLocationCountry (ISO alpha-2, nullable)
 │  -- all four extracted together in one LLM call per posting, see FoundryJobPostingClassifier

MatchResults
 ├─ Id (PK), UserId (FK), JobPostingId (FK), CandidateProfileId (FK)
 ├─ UNIQUE (UserId, JobPostingId)
 ├─ VectorScore (float, cosine distance), LlmScore (float 0–100), LlmReasoning (text)
 ├─ Status (enum: PendingReview | Notified | Dismissed)
 ├─ CreatedAtUtc, NotifiedAtUtc (nullable)

Applications
 ├─ Id (PK), UserId (FK), MatchResultId (FK)
 ├─ Status (enum: Matched | Prepped | Applied | Withdrawn)
 ├─ TailoredResumeBlobUrl, TailoredCoverLetterBlobUrl (nullable until generated)
 ├─ GeneratedAtUtc, AppliedAtUtc (nullable), Notes

PollRunLogs                             -- ops/debug audit trail
 ├─ Id (PK), JobSourceSubscriptionId (FK)
 ├─ StartedAtUtc, CompletedAtUtc, JobsFetched, JobsNew, JobsFailed, ErrorMessage (nullable)
```

Vector columns: `SqlVector<float>` used only in WHERE/ORDER BY via `EF.Functions.VectorDistance` — never needed in default read projections. `ConfigJson` on `JobSourceSubscriptions` uses the native `json` column type rather than parallel nullable columns because Greenhouse/Lever ("which company board") and Adzuna ("what keywords/location") have genuinely different shapes.

**Known dedup limitation (cross-source duplicates)**: `(Source, ExternalJobId)` only catches re-polls of the *same* source. The same job surfacing via both Adzuna and Greenhouse/Lever creates two rows → two matches → two notifications. Accepted for v1 (annoyance, not corruption). Cheap mitigation when it starts to hurt: fuzzy `(CompanyName, Title)` similarity check in the notify step to suppress near-duplicate notifications — do NOT try to merge the rows themselves.

## 4. `IJobSourceClient` abstraction

```csharp
public interface IJobSourceClient
{
    JobSource Source { get; }   // Greenhouse | Lever | Adzuna

    Task<JobFetchResult> FetchJobsAsync(
        JobSourceSubscription subscription,
        JobFetchCursor? cursor,
        CancellationToken ct);
}

public sealed record JobFetchResult(IReadOnlyList<RawJobPosting> Jobs, JobFetchCursor? NextCursor, bool HasMore);

public sealed record RawJobPosting(
    string ExternalJobId, string Title, string CompanyName, string? LocationText,
    string DescriptionText, string ApplyUrl, DateTimeOffset? PostedAtUtc, string RawJson);

public sealed record JobFetchCursor(int? Page, string? OpaqueToken); // source-specific meaning
```

Greenhouse/Lever are per-company job-board APIs (no cross-employer keyword search — you must know the board token/slug). Adzuna is a keyword+location search aggregator across many employers, paginated. `JobSourceSubscription.ConfigJson` tells each adapter *what* to poll; the interface stays uniform because per-source differences are fully encapsulated:

- **GreenhouseJobSourceClient** — `GET boards-api.greenhouse.io/v1/boards/{boardToken}/jobs?content=true`. Full list in one call, `HasMore` always `false`, cursor unused. `id → ExternalJobId`, `content → DescriptionText` (HTML, strip tags before embedding/LLM), `absolute_url → ApplyUrl`.
- **LeverJobSourceClient** — `GET api.lever.co/v0/postings/{company}?mode=json`. Also single full list, no pagination. `id`, `text → Title`, `categories.location → LocationText`, `descriptionPlain`/`additionalPlain → DescriptionText`, `hostedUrl → ApplyUrl`.
- **AdzunaJobSourceClient** — `GET api.adzuna.com/v1/api/jobs/{country}/search/{page}?app_id=&app_key=&what=&where=&results_per_page=`. Genuinely paginated — `cursor.Page` drives `{page}`, `HasMore` computed from `count`/`results_per_page`. Requires `app_id`/`app_key` (free-tier creds), unlike GH/Lever's keyless public APIs.

Each adapter registered as a named `HttpClient` via `IHttpClientFactory` + `.AddStandardResilienceHandler()` (retry w/ jittered backoff on 5xx/429/timeout, circuit breaker, timeout) applied per source so one failing source can't starve the others. Adding a 4th source later = one new adapter + one new `JobSource` enum value, no changes to polling/dedupe/embedding/matching code.

## 5. Background polling service

`JobPollingBackgroundService : BackgroundService`, lives in `JobApplyAi.Api`, registered alongside the Web API in the same App Service process (no Azure Functions). `PeriodicTimer` ticks on a short base interval (e.g. 5 min) but each subscription is only fetched when its **per-source cadence** is due (`Polling:Sources:{Source}:IntervalMinutes`), tracked via `LastPolledAtUtc`:

- **Greenhouse / Lever**: keyless public APIs, cheap — hourly is fine.
- **Adzuna**: free tier is quota-limited (~250 calls/day) and each poll can span multiple pages — poll 2–4×/day (e.g. `IntervalMinutes: 360`) with a page cap, or the quota burns out mid-month. A single global interval would silently exhaust it.

**Hosting requirement**: App Service **Always On must be enabled** (requires Basic tier or higher — not available on Free/Shared). Without it, the idle app unloads after ~20 min and the timer silently stops. This is the operational cost of choosing in-process `BackgroundService` over Azure Functions; it's a hard prerequisite, not an optimization.

Per tick:
1. **Load subscriptions** — enabled `JobSourceSubscriptions` for `SeedData.DefaultUserId`.
2. **Fetch** — resolve the matching `IJobSourceClient` per subscription, call inside the Polly-wrapped `HttpClient`, loop pagination (Adzuna) until `HasMore == false` or a page-cap safety limit. Isolate failures per-subscription (try/catch, log, `LastPollStatus = Error`, continue) — one bad source never blocks the others.
3. **Dedupe** — bulk-load existing `(Source, ExternalJobId)` keys for touched sources, filter in-memory before insert.
4. **Persist new postings** — insert new `JobPosting` rows, embedding left null.
5. **Embed** — batch-call the Foundry embedding model (`IEmbeddingGenerator<string, Embedding<float>>`) for postings/profile missing embeddings (description text cleaned/truncated first). Populate `SqlVector<float>`, save.
6. **Vector prefilter** — `EF.Functions.VectorDistance("cosine", jp.JobEmbedding, profile.ProfileEmbedding)` ascending, `Take(TopN)` (config, e.g. 25), excluding postings that already have a `MatchResult` for this user.
7. **LLM rescore** — per candidate, Foundry chat call with profile + job description, JSON-mode response `{ score: 0-100, reasoning }`. Upsert `MatchResult` (`Status = PendingReview`).
8. **Notify** — `MatchResults` with `LlmScore >= Matching:NotifyThreshold` and `Status == PendingReview` → email via `IEmailNotifier` (MailKit/Gmail), `Status = Notified`, `NotifiedAtUtc` set. No separate notification table for v1 — dashboard queries `MatchResults` by status directly.
9. **Log** — `PollRunLog` row per subscription (fetched/new/failed counts, timing).

Steps 5–8 run once per tick across all subscriptions' new postings together (keyed off the single active `CandidateProfile`, not per-source).

## 6. Resume upload → parse → review → save flow

- `POST /api/profile/resume` (multipart PDF/DOCX) → uploads to Blob `resumes/raw/{userId}/{profileId}/{filename}`, creates `CandidateProfile` (`Status = Parsing`), returns `202 Accepted` + `{ profileId }` immediately — parse runs async (queued background work), doesn't block the request.
  - **Stuck-state recovery**: fire-and-forget parse work dies silently if the app restarts mid-parse, leaving the profile in `Parsing` forever. The polling service's tick (or the status endpoint) marks any profile stuck in `Parsing` longer than `Parsing:TimeoutMinutes` (e.g. 10) as `Failed` so the user can re-upload instead of waiting on a ghost.
- `GET /api/profile/{id}/status` → `{ status }`, client polls until `NeedsReview`/`Failed`.
- `GET /api/profile/{id}` → full structured profile for the review screen.
- `PUT /api/profile/{id}` → user-edited save; replace-all semantics for child collections (delete+reinsert `ProfileWorkExperiences`/`Educations`/`Skills` — simplest correct approach at this volume).
- `POST /api/profile/{id}/confirm` → `Status = Active`, generates `ProfileEmbedding` from the approved summary+experience text, sets any prior `Active` profile to `Superseded` (exactly one active profile at a time).
- `GET /api/profile/active` → convenience read for dashboard/extension use.

## 7. Browser extension ↔ API contract

Manifest V3, content scripts on `boards.greenhouse.io`/`job-boards.greenhouse.io` and `jobs.lever.co` only (v1 scope). Calls the API directly over REST/HTTPS, no server-side proxy.

**All API calls route through the service worker, never fetched from content scripts.** MV3 content-script fetches carry the *host page's* origin (`boards.greenhouse.io`), not `chrome-extension://<id>` — the extension origin only appears on service-worker fetches. So: content script extracts the job id from the page → `chrome.runtime.sendMessage` to the service worker → service worker fetches the API (with the `X-Api-Key` header) → response message-passed back. `lib/api-client.ts` lives on the service-worker side; the API host goes in `host_permissions` in the manifest.

**Expectation note — Adzuna matches mostly aren't autofillable.** Adzuna apply URLs land on arbitrary external pages (Indeed, company sites, other ATSs), which the v1 content scripts don't cover. Adzuna's role is broad *discovery/notification*; one-click-ish autofill only works when the posting lives on a Greenhouse/Lever page. Document this in the dashboard UI so a non-autofillable match isn't read as a bug.

- `GET /api/applications/by-external-job/{source}/{externalJobId}` → content script extracts the ATS-native job id from the page URL, gets back (actual shape as built, `ApplicationEndpoints.ExtensionContextDto` — trimmed from the original sketch below: `workHistory`/`education` were speculative and got cut once built, since no content script fills anything from them; add back if a real consumer needs them):
  ```json
  {
    "jobPosting": { "title": "", "companyName": "" } | null,
    "matchResultId": "guid | null",
    "applicationId": "guid | null",
    "applicationStatus": "Prepped | Applied | null",
    "profile": {
      "firstName": "", "lastName": "", "email": "", "phone": "",
      "linkedInUrl": "", "portfolioUrl": "", "locationText": ""
    } | null,
    "resumeDownloadUrl": "sasUrl | null",
    "coverLetterDownloadUrl": "sasUrl | null"
  }
  ```
  Field names are pre-normalized/generic; the content script's field-mapper matches them against each ATS's DOM labels — no ATS-specific shape leaks into the API response.
- `POST /api/applications/{jobPostingId}/generate` (from the web dashboard, not the extension) → kicks off Foundry tailored resume+cover-letter generation, uploads to Blob (`generated/{userId}/{applicationId}/resume.pdf`, `cover-letter.pdf`), creates/updates `Application`, `Status = Prepped`.
- `POST /api/applications/{id}/status` → `{ status: "Applied" }`, sent after the user manually submits and confirms in the extension popup. Optionally also `{ event: "AutofillCompleted", fieldsFilled: [...] }` telemetry (debugging aid, not required for correctness).
- Documents are never proxied through the API — the context endpoint returns a short-lived Blob SAS URL directly.
- File-attachment limitation (browsers block scripting `input[type=file].files`): resume/cover-letter is pre-generated + downloadable, content script autofills every other field and visibly flags the file input with a "download & attach manually" prompt. Not attempting `chrome.debugger` tricks in v1 — documented in `extension/README.md`.

## 8. CORS & security (no-auth v1)

- **CORS**: named policy allowlisting only the exact `chrome-extension://<extension-id>` origin (fixed once known). The Blazor Server dashboard is same-origin/in-process — it needs no CORS entry at all. No `AllowAnyOrigin` — this API holds real PII (parsed resume, contact info). Note: service-worker fetches with `host_permissions` granted often bypass CORS anyway, but the allowlist stays configured as belt-and-suspenders and for any future SPA origin.
- **Shared-secret API key**: no user auth exists, so the *entire* API requires a static `X-Api-Key` header, checked by middleware, key stored in App Service Configuration/Key Vault (never source-controlled). Applies to web dashboard and extension calls alike, since the App Service URL is public. `/health` exempted for App Service probes.
- **Rate limiting**: `Microsoft.AspNetCore.RateLimiting`, basic fixed-window limiter — defense-in-depth against a leaked key, not sophisticated-attacker-grade.
- **Blob access**: private containers only, no anonymous access, documents served exclusively via short-lived SAS URLs minted by the API.
- **Forward path to real auth**: the API-key middleware is a deliberate stand-in. `UserId` threading through every table means swapping the key check for real per-user auth (JWT/Entra ID) later is additive — replace the middleware, resolve `UserId` from claims instead of `SeedData.DefaultUserId` — not a data migration.

## 9. Build order / milestones

0. **Azure provisioning** (prerequisite, not code) — Azure SQL Database, Blob Storage account (private containers), Foundry project with chat + embedding model deployments, App Service plan **Basic tier or higher** (Always On). Milestone 1's verification step can't run without the database existing.
1. **Solution scaffold + DB schema + migrations** — 3 projects + test projects, `Directory.Build.props`/`global.json` pinning `net10.0`, Blazor Server wired into Api, `AppDbContext` with §3 entities, `UseAzureSql(...)`, initial migration incl. seeded `Users` row. Verify `vector`/`json` columns actually create against a real Azure SQL Database (or local SQL Server 2025 container) before building on top.
2. **Resume upload → parse → review → save flow** — Blob upload wiring, Foundry document-parsing integration (verify exact SDK package names here), §6 endpoints incl. stuck-`Parsing` recovery, Blazor review/edit screen.
3. **Job source adapters + polling + dedupe** — `IJobSourceClient` + 3 adapters (§4) with WireMock adapter tests, `JobSourceSubscription` CRUD (seeded config rows OK for v1), `JobPollingBackgroundService` steps 1–4 only (fetch/dedupe/persist, no AI yet) with per-source cadence, to prove the loop end-to-end. **Security hardening (API key middleware, rate limiting, CORS) lands here at the latest** — App Service is public from first deploy. *(Milestones 2 and 3 are independent — parallelizable or reorderable.)*
4. **Embeddings + vector prefilter + LLM rescore** — `IEmbeddingGenerator` wiring, populate embeddings, `VectorDistance` prefilter, LLM rescore + `MatchResult` upsert (steps 5–7). Needs both M2 (active profile) and M3 (postings). **Extended beyond the original plan** with hard matching filters (visa sponsorship, salary floor, excluded companies) — added mid-build once live-tested and found to be a real gap, see Status section above.
5. **Notifications** — MailKit/Gmail integration, notify step, Blazor dashboard page listing `MatchResults` by status.
6. **Tailored resume/cover-letter generation** — `POST /api/applications/{jobPostingId}/generate`, Foundry generation, QuestPDF rendering to PDF, Blob upload, `Application` lifecycle.
7. **Browser extension + autofill** — manifest, content scripts for Greenhouse/Lever field mapping + natural-typing-event simulation, popup UI, §7 contract, §8 CORS/API-key.
8. **End-to-end test** — real resume through the full loop: upload → review → poll picks up a real posting → embed/prefilter/rescore → email → generate tailored docs → extension autofill on the live ATS page → manual submit → status update.

### Critical files (once milestone 1 starts)

- `src/JobApplyAi.Infrastructure/Data/AppDbContext.cs` — EF Core model, `UseAzureSql`, vector column config
- `src/JobApplyAi.Domain/Abstractions/IJobSourceClient.cs` — adapter contract
- `src/JobApplyAi.Api/BackgroundServices/JobPollingBackgroundService.cs` — poll→dedupe→embed→prefilter→rescore→notify loop
- `src/JobApplyAi.Api/Program.cs` — composition root: DI, HttpClients+resilience, Foundry clients, CORS, API-key middleware, hosted service registration
- `extension/manifest.json` + `extension/src/lib/api-client.ts` — extension origin (drives CORS allowlist) + REST contract shape
