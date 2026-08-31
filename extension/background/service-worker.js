// Runs in the extension's own origin — content-script fetches carry the HOST PAGE's origin
// (e.g. boards.greenhouse.io), not chrome-extension://<id>, so all API calls must be relayed
// through here. See CLAUDE.md "Extension fetches go through the service worker only".

async function getConfig() {
  const { apiBaseUrl, apiKey } = await chrome.storage.sync.get(['apiBaseUrl', 'apiKey']);
  return { apiBaseUrl: (apiBaseUrl || 'https://localhost:7010').replace(/\/$/, ''), apiKey: apiKey || '' };
}

async function apiFetch(path, options = {}) {
  const { apiBaseUrl, apiKey } = await getConfig();
  if (!apiKey) {
    throw new Error('No API key set — open the extension popup and configure it.');
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...options,
    headers: {
      'X-Api-Key': apiKey,
      'Content-Type': 'application/json',
      ...(options.headers || {}),
    },
  });

  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.error || `Request failed: HTTP ${response.status}`);
  }
  return body;
}

chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
  (async () => {
    try {
      if (message.type === 'GET_CONTEXT') {
        const data = await apiFetch(`/api/applications/by-external-job/${message.source}/${encodeURIComponent(message.externalJobId)}`);
        sendResponse({ ok: true, data });
      } else if (message.type === 'MARK_APPLIED') {
        const data = await apiFetch(`/api/applications/${message.matchResultId}/mark-applied`, { method: 'POST' });
        sendResponse({ ok: true, data });
      } else {
        sendResponse({ ok: false, error: `Unknown message type: ${message.type}` });
      }
    } catch (err) {
      sendResponse({ ok: false, error: err.message });
    }
  })();
  return true; // keep the async channel open for sendResponse
});
