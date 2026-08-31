// Shared by both ATS content scripts. Label-text matching is the primary strategy (more robust
// across per-company form customization than hardcoded CSS selectors); a couple of common
// id/name guesses are layered in as fast paths, not sole reliance.

function jobApplyAiSetNativeValue(element, value) {
  // Both Greenhouse's job-boards.greenhouse.io and Lever forms are React-controlled — setting
  // .value directly doesn't register with React's internal state. Going through the prototype's
  // native setter and dispatching input/change events is the standard workaround.
  const prototype = Object.getPrototypeOf(element);
  const prototypeValueSetter = Object.getOwnPropertyDescriptor(prototype, 'value')?.set;
  const ownValueSetter = Object.getOwnPropertyDescriptor(element, 'value')?.set;

  if (prototypeValueSetter && ownValueSetter !== prototypeValueSetter) {
    prototypeValueSetter.call(element, value);
  } else {
    element.value = value;
  }

  element.dispatchEvent(new Event('input', { bubbles: true }));
  element.dispatchEvent(new Event('change', { bubbles: true }));
}

function jobApplyAiFindFieldByLabel(pattern) {
  const labels = Array.from(document.querySelectorAll('label'));
  for (const label of labels) {
    if (!pattern.test(label.textContent || '')) continue;
    const forId = label.getAttribute('for');
    if (forId) {
      const el = document.getElementById(forId);
      if (el) return el;
    }
    // Prefer text-entry controls over <select> when a label wraps more than one control (e.g.
    // "Phone" wrapping a country-code <select> plus the actual number <input>) — every field we
    // fill (name/email/phone/links/location) is a text value, never legitimately a <select>.
    const nested = label.querySelector('input:not([type=hidden])') || label.querySelector('textarea') || label.querySelector('select');
    if (nested) return nested;
  }
  return null;
}

function jobApplyAiFindField(selectors) {
  for (const selector of selectors) {
    if (selector.type === 'css') {
      const el = document.querySelector(selector.value);
      if (el) return el;
    } else if (selector.type === 'label') {
      const el = jobApplyAiFindFieldByLabel(selector.value);
      if (el) return el;
    }
  }
  return null;
}

function jobApplyAiFillField(selectors, value) {
  if (value === null || value === undefined || value === '') return false;
  const el = jobApplyAiFindField(selectors);
  if (!el) return false;
  jobApplyAiSetNativeValue(el, value);
  return true;
}

function jobApplyAiCreateBanner() {
  const existing = document.getElementById('jobapplyai-banner');
  if (existing) return existing;

  const banner = document.createElement('div');
  banner.id = 'jobapplyai-banner';
  banner.style.cssText =
    'position:fixed;top:0;left:0;right:0;z-index:2147483647;padding:10px 16px;' +
    'font-family:system-ui,sans-serif;font-size:13px;background:#1a1a2e;color:#fff;';
  document.body.prepend(banner);
  return banner;
}

function jobApplyAiSetBannerText(banner, text) {
  banner.textContent = `JobApplyAi: ${text}`;
}

window.JobApplyAiFieldMapper = {
  fillField: jobApplyAiFillField,
  findField: jobApplyAiFindField,
  setNativeValue: jobApplyAiSetNativeValue,
};

window.JobApplyAiUi = {
  createBanner: jobApplyAiCreateBanner,
  setBannerText: jobApplyAiSetBannerText,
};
