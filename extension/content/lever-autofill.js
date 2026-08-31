(function () {
  // /{company}/{postingId} or /{company}/{postingId}/apply — postingId is a long UUID-ish token.
  const match = window.location.pathname.match(/^\/[^/]+\/([a-f0-9-]{20,})/i);
  if (!match) return;
  const externalJobId = match[1];

  const banner = window.JobApplyAiUi.createBanner();
  window.JobApplyAiUi.setBannerText(banner, 'looking up your profile…');

  chrome.runtime.sendMessage({ type: 'GET_CONTEXT', source: 'Lever', externalJobId }, (response) => {
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
    const fullName = [p.firstName, p.lastName].filter(Boolean).join(' ');
    const mapper = window.JobApplyAiFieldMapper;
    let filled = 0;
    // Lever's standard apply form uses fairly consistent field names across companies (unlike
    // Greenhouse, which is more customized per board), so CSS selectors are the primary path here.
    const attempts = [
      [[{ type: 'css', value: 'input[name="name"]' }, { type: 'label', value: /^(full )?name$/i }], fullName],
      [[{ type: 'css', value: 'input[name="email"]' }, { type: 'label', value: /^email/i }], p.email],
      [[{ type: 'css', value: 'input[name="phone"]' }, { type: 'label', value: /phone/i }], p.phone],
      [[{ type: 'css', value: 'input[name="urls[LinkedIn]"]' }, { type: 'label', value: /linkedin/i }], p.linkedInUrl],
      [[{ type: 'css', value: 'input[name="urls[Portfolio]"]' }, { type: 'label', value: /portfolio|website/i }], p.portfolioUrl],
      [[{ type: 'css', value: 'input[name="location"]' }, { type: 'label', value: /\b(location|city)\b/i }], p.locationText],
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

    window.__jobApplyAiContext = data;
  });
})();
