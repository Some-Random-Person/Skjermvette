/** What the extension reports to the Windows agent's Helper (`BrowserReport` in the Helper). */
export interface BrowserReport {
  /** The browser's program, so the Helper knows which browser in front or making sound this is. */
  browser: string;
  /** The website in the active tab of the last focused window, or null for pages that aren't websites. */
  active: string | null;
  /** The websites in tabs making sound (not muted). */
  audible: string[];
}

/** The same check as the Helper's (`BrowserPipe.Domain`), which refuses a report with anything else. */
const domain = /^[a-z0-9.-]{1,253}$/;

/** The website (host name) of a tab's URL, or null for anything that isn't a website, e.g. a new tab or settings. */
export function domainOf(url: string | undefined): string | null {
  if (!url) return null;
  try {
    const { protocol, hostname } = new URL(url);
    return (protocol === "http:" || protocol === "https:") && domain.test(hostname)
      ? hostname
      : null;
  } catch {
    return null;
  }
}

export function reportOf(
  browser: string,
  active: chrome.tabs.Tab | undefined,
  audible: chrome.tabs.Tab[],
): BrowserReport {
  const playing = audible.filter((tab) => !tab.mutedInfo?.muted).map((tab) => domainOf(tab.url));
  return {
    browser,
    active: domainOf(active?.url),
    audible: [...new Set(playing.filter((d) => d !== null))].slice(0, 32),
  };
}
