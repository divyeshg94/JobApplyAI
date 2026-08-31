async function loadSettings() {
  const { apiBaseUrl, apiKey } = await chrome.storage.sync.get(['apiBaseUrl', 'apiKey']);
  document.getElementById('apiBaseUrl').value = apiBaseUrl || '';
  document.getElementById('apiKey').value = apiKey || '';
}

document.getElementById('save').addEventListener('click', async () => {
  const apiBaseUrl = document.getElementById('apiBaseUrl').value.trim().replace(/\/$/, '');
  const apiKey = document.getElementById('apiKey').value.trim();
  await chrome.storage.sync.set({ apiBaseUrl, apiKey });
  document.getElementById('status').textContent = 'Saved. Reload the job page to pick it up.';
});

async function renderActiveTabContext() {
  const container = document.getElementById('context');
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.id) return;

  let results;
  try {
    results = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: () => window.__jobApplyAiContext || null,
    });
  } catch {
    return; // not an ATS page the content script ran on — nothing to show
  }

  const data = results?.[0]?.result;
  if (!data) return;

  container.innerHTML = '';
  const title = document.createElement('div');
  title.textContent = data.jobPosting ? `${data.jobPosting.title} — ${data.jobPosting.companyName}` : 'This page';
  container.appendChild(title);

  if (data.resumeDownloadUrl) {
    const resumeLink = document.createElement('a');
    resumeLink.href = data.resumeDownloadUrl;
    resumeLink.target = '_blank';
    resumeLink.textContent = 'Download tailored resume';
    container.appendChild(resumeLink);
    container.appendChild(document.createElement('br'));
  }
  if (data.coverLetterDownloadUrl) {
    const coverLetterLink = document.createElement('a');
    coverLetterLink.href = data.coverLetterDownloadUrl;
    coverLetterLink.target = '_blank';
    coverLetterLink.textContent = 'Download tailored cover letter';
    container.appendChild(coverLetterLink);
    container.appendChild(document.createElement('br'));
  }

  if (data.matchResultId && data.applicationStatus !== 'Applied') {
    const markAppliedButton = document.createElement('button');
    markAppliedButton.textContent = 'Mark applied';
    markAppliedButton.addEventListener('click', () => {
      chrome.runtime.sendMessage({ type: 'MARK_APPLIED', matchResultId: data.matchResultId }, (response) => {
        markAppliedButton.textContent = response?.ok ? 'Marked applied' : `Failed: ${response?.error}`;
        markAppliedButton.disabled = !!response?.ok;
      });
    });
    container.appendChild(markAppliedButton);
  } else if (data.applicationStatus === 'Applied') {
    const applied = document.createElement('div');
    applied.textContent = 'Already marked applied.';
    container.appendChild(applied);
  }
}

loadSettings();
renderActiveTabContext();
