(function () {
  const match = window.location.pathname.match(/\/jobs\/(\d+)/);
  if (!match) return; // not a job posting page (e.g. board listing page)
  const externalJobId = match[1];

  const banner = window.JobApplyAiUi.createBanner();
  window.JobApplyAiUi.setBannerText(banner, 'looking up your profile…');

  chrome.runtime.sendMessage({ type: 'GET_CONTEXT', source: 'Greenhouse', externalJobId }, (response) => {
    if (chrome.runtime.lastError || !response || !response.ok) {
      const message = response?.error || chrome.runtime.lastError?.message || 'unknown error';
      window.JobApplyAiUi.setBannerText(banner, `error — ${message}. Check the extension popup for API key/URL setup.`);
      return;
    }

    const { data } = response;
    if (!data.profile) {
      window.JobApplyAiUi.setBannerText(banner, 'no active profile found — activate one in JobApplyAi first.');
      return;
    }

    const p = data.profile;
    const mapper = window.JobApplyAiFieldMapper;
    let filled = 0;
    const attempts = [
      [[{ type: 'css', value: '#first_name' }, { type: 'label', value: /first name/i }], p.firstName],
      [[{ type: 'css', value: '#last_name' }, { type: 'label', value: /last name/i }], p.lastName],
      [[{ type: 'css', value: '#email' }, { type: 'label', value: /^email/i }], p.email],
      [[{ type: 'css', value: '#phone' }, { type: 'label', value: /phone/i }], p.phone],
      [[{ type: 'label', value: /linkedin/i }], p.linkedInUrl],
      [[{ type: 'label', value: /portfolio|website/i }], p.portfolioUrl],
      [[{ type: 'label', value: /\b(location|city)\b/i }], p.locationText],
    ];
    for (const [selectors, value] of attempts) {
      if (mapper.fillField(selectors, value)) filled += 1;
    }

    let statusText = `filled ${filled} field(s).`;
    const fileInput = document.querySelector('input[type=file]');
    if (fileInput && data.resumeDownloadUrl) {
      statusText += ' Resume/cover letter ready in the popup — download & attach manually (browsers block auto-attaching files).';
    }
    statusText += ' Review everything, then click Submit yourself — this never auto-submits.';
    window.JobApplyAiUi.setBannerText(banner, statusText);

    // Popup reads this via chrome.scripting.executeScript when opened on this tab.
    window.__jobApplyAiContext = data;
  });
})();
