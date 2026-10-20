/**
 * Which websites to block, as the Windows agent sends them (`WebsiteBlocks` in Core). A domain includes its subdomains,
 * and the most specific domain listed wins. Any other website is blocked only when `blockOthers` is set.
 */
export interface WebsiteBlocks {
  /** Set when the daily total is used up. */
  blockOthers: boolean;
  /** `app` is the app whose own limit is used up, or null when it's the daily total. */
  blocked: { domain: string; app: string | null }[];
  /** Domains never blocked: apps with time left, exempt apps and the dashboard. */
  allowed: string[];
}

/** The same check as the agent's, so a bad domain can't make the browser refuse the whole list. */
const domain = /^[a-z0-9.-]{1,253}$/;

/** The list in a message from the agent, or null if it isn't one. */
export function websiteBlocksOf(message: unknown): WebsiteBlocks | null {
  const m = message as Partial<WebsiteBlocks> | null;
  const valid =
    typeof m?.blockOthers === "boolean" &&
    Array.isArray(m.blocked) &&
    m.blocked.every(
      (b) => domain.test(b?.domain) && (b.app === null || typeof b.app === "string"),
    ) &&
    Array.isArray(m.allowed) &&
    m.allowed.every((d) => domain.test(d));
  return valid ? (m as WebsiteBlocks) : null;
}

/** The blocked page, naming the app whose limit is used up (none for the daily total). */
export function blockedPath(app: string | null): string {
  return app === null ? "/blocked.html" : `/blocked.html?app=${encodeURIComponent(app)}`;
}

/** A more specific domain gets a higher priority, so it wins, as in the agent. */
function priorityOf(d: string): number {
  return d.split(".").length + 1;
}

/** Rules that redirect blocked pages to the blocked page. Only pages are blocked, not what they load. */
export function rulesOf(blocks: WebsiteBlocks): chrome.declarativeNetRequest.Rule[] {
  const resourceTypes = ["main_frame"] as chrome.declarativeNetRequest.ResourceType[];
  const rules: Omit<chrome.declarativeNetRequest.Rule, "id">[] = [
    ...blocks.blocked.map((b) => ({
      priority: priorityOf(b.domain),
      action: {
        type: "redirect",
        redirect: { extensionPath: blockedPath(b.app) },
      } as chrome.declarativeNetRequest.RuleAction,
      condition: { requestDomains: [b.domain], resourceTypes },
    })),
    // At the same priority an allow rule wins over a redirect.
    ...blocks.allowed.map((d) => ({
      priority: priorityOf(d),
      action: { type: "allow" } as chrome.declarativeNetRequest.RuleAction,
      condition: { requestDomains: [d], resourceTypes },
    })),
  ];
  if (blocks.blockOthers)
    rules.push({
      priority: 1,
      action: {
        type: "redirect",
        redirect: { extensionPath: blockedPath(null) },
      } as chrome.declarativeNetRequest.RuleAction,
      // "|http" is any address starting with http, so https too.
      condition: { urlFilter: "|http", resourceTypes },
    });
  return rules.map((rule, i) => ({ ...rule, id: i + 1 }));
}

/** Where a page on `host` goes when blocked, or null when it isn't: the same decision as the rules. */
export function blockedPathFor(blocks: WebsiteBlocks, host: string): string | null {
  const matches = (d: string) => host === d || host.endsWith(`.${d}`);
  const blocked = blocks.blocked.filter((b) => matches(b.domain));
  const allowed = blocks.allowed.filter(matches);
  const mostSpecific = (ds: string[]) => Math.max(0, ...ds.map(priorityOf));
  const block = mostSpecific(blocked.map((b) => b.domain));
  const allow = mostSpecific(allowed);
  if (block > 0 && block > allow) {
    const winner = blocked.find((b) => priorityOf(b.domain) === block)!;
    return blockedPath(winner.app);
  }
  return allow === 0 && blocks.blockOthers ? blockedPath(null) : null;
}
