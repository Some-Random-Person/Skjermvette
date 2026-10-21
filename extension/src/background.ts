import { blockedPathFor, rulesOf, websiteBlocksOf, type WebsiteBlocks } from "./blocking.js";
import { domainOf, reportOf } from "./report.js";

/** The native messaging host: the Windows agent's Helper, registered by the agent under this name. */
const host = "no.skjermvette.agent";
/** Edge says so in its user agent; Chrome is anything else. */
const browser = navigator.userAgent.includes(" Edg/") ? "msedge.exe" : "chrome.exe";
const retryMs = 2000;

let port: chrome.runtime.Port | undefined;
let last = "";
/** Set while waiting to connect again, so tab changes in the meantime don't each start a connection. */
let retry: ReturnType<typeof setTimeout> | undefined;
/** The list last applied, and the one being applied, so lists are applied one at a time and in order. */
let applied = "";
let blocking = Promise.resolve();

function connect(): chrome.runtime.Port {
  const connected = chrome.runtime.connectNative(host);
  connected.onMessage.addListener((message) => {
    const blocks = websiteBlocksOf(message);
    if (!blocks) return console.warn("Not a list of websites to block:", message);
    blocking = blocking.then(() => block(blocks)).catch((e) => console.error("Not blocked:", e));
  });
  connected.onDisconnect.addListener(() => {
    console.warn("Not connected to the Skjermvette agent:", chrome.runtime.lastError?.message);
    port = undefined;
    retry ??= setTimeout(() => {
      retry = undefined;
      void report();
    }, retryMs);
  });
  return connected;
}

/** Sends what's in use now, when it has changed or after connecting again. */
async function report(): Promise<void> {
  const [active] = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  const audible = await chrome.tabs.query({ audible: true });
  const message = reportOf(browser, active, audible);
  const json = JSON.stringify(message);
  if ((port && json === last) || (!port && retry)) return;

  port ??= connect();
  port.postMessage(message);
  last = json;
}

/**
 * Blocks what the agent says, and moves tabs already open on a blocked website (including ones playing in the
 * background) to the blocked page. The browser keeps the rules, so the last list stays in force while the agent can't
 * be reached, also after a restart.
 */
async function block(blocks: WebsiteBlocks): Promise<void> {
  const json = JSON.stringify(blocks);
  if (json === applied) return;
  const old = await chrome.declarativeNetRequest.getDynamicRules();
  await chrome.declarativeNetRequest.updateDynamicRules({
    removeRuleIds: old.map((rule) => rule.id),
    addRules: rulesOf(blocks),
  });
  applied = json;

  for (const tab of await chrome.tabs.query({ url: ["http://*/*", "https://*/*"] })) {
    const domain = domainOf(tab.url);
    const path = domain && blockedPathFor(blocks, domain);
    if (tab.id !== undefined && path)
      await chrome.tabs.update(tab.id, { url: chrome.runtime.getURL(path) });
  }
}

// While connected the browser keeps this worker running. When it isn't, the alarm wakes it to connect again.
chrome.alarms.create("report", { periodInMinutes: 0.5 });
chrome.alarms.onAlarm.addListener(() => void report());
chrome.runtime.onStartup.addListener(() => void report());
chrome.runtime.onInstalled.addListener(() => void report());
chrome.tabs.onActivated.addListener(() => void report());
chrome.tabs.onRemoved.addListener(() => void report());
chrome.tabs.onUpdated.addListener((_, change) => {
  if (change.url || change.audible !== undefined || change.mutedInfo) void report();
});
chrome.windows.onFocusChanged.addListener(() => void report());
void report();
