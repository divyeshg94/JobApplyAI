# JobApplyAi Autofill (Chrome extension)

Manifest V3, plain JS, no build step — load it unpacked and edit files directly, reload the
extension to pick up changes. (Deviates from the original TS+vite sketch in the architecture doc:
plain JS was simpler to iterate on for v1 and needs zero tooling to load.)

## What it does

On a Greenhouse (`boards.greenhouse.io` / `job-boards.greenhouse.io`) or Lever (`jobs.lever.co`)
job posting page, it:

1. Reads the job's ID from the URL, asks the JobApplyAi API for your active profile + (if this
   posting was already matched and you generated documents) the tailored resume/cover letter.
2. Autofills name, email, phone, LinkedIn, portfolio, location into the application form.
3. Shows a status banner with what it filled and what's still manual.

**It never submits the form and never attaches the resume/cover letter file for you** — both are
deliberate limitations, not bugs:

- Browsers block scripts from setting `input[type=file].files` for security reasons. The banner
  tells you the tailored files are ready in the extension popup; you download and attach them
  yourself, then click Submit. Not attempting `chrome.debugger` workarounds for this in v1.
- Human always clicks Submit — see CLAUDE.md.

## Load it (local dev)

1. `chrome://extensions` → enable **Developer mode** (top right) → **Load unpacked** → select this
   `extension/` folder.
2. Click the extension icon → set **API base URL** (default `https://localhost:7010`, matches the
   app's local HTTPS profile) and **API key** (same value as the `ApiKey` user-secret) → **Save**.
3. Navigate to a real Greenhouse or Lever job posting page and reload it.

If your API isn't on `https://localhost:7010` (different port, or a deployed App Service URL),
edit the `host_permissions` entry in `manifest.json` to match before loading — Chrome extensions
can't fetch a host they weren't granted permission for, regardless of what's typed into the popup.
Re-load the unpacked extension after editing the manifest.

## Known limitations (v1)

- Field selectors are best-effort (label-text matching first, a few common id/name guesses as fast
  paths). Heavily customized per-company forms may need selector tweaks in `content/*.js`.
- Lever job-ID extraction assumes the standard `/{company}/{postingId}` URL shape; custom Lever
  domains aren't handled.
- No packaging/publishing — unpacked/local only, matches the "no CLI, solo tool" scope of v1.
