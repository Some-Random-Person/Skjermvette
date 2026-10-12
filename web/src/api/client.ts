import { t } from "../i18n/t";
import type {
  App,
  Child,
  ChildRequest,
  Device,
  Identifier,
  MatcherKind,
  Me,
  PairingCode,
  Today,
  UnassignedIdentifier,
} from "./types";

/** An error response from the server, with the most useful message from its ProblemDetails. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

/** Called on any 401, so the app can return to the sign-in page. */
let onUnauthorized: () => void = () => {};
export function setUnauthorizedHandler(handler: () => void) {
  onUnauthorized = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const response = await fetch(path, {
    method,
    credentials: "same-origin",
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (response.status === 401 && !path.startsWith("/api/auth/")) onUnauthorized();
  if (!response.ok) throw new ApiError(response.status, await errorMessage(response));
  if (response.status === 204 || response.headers.get("Content-Length") === "0")
    return undefined as T;
  return (await response.json()) as T;
}

async function errorMessage(response: Response): Promise<string> {
  try {
    const problem = await response.json();
    const validation = problem.errors ? Object.values(problem.errors).flat() : [];
    return validation.length > 0 ? validation.join(" ") : (problem.detail ?? problem.title);
  } catch {
    return t("common.error");
  }
}

export const api = {
  me: () => request<Me>("GET", "/api/auth/me"),
  login: (email: string, password: string) =>
    request<Me>("POST", "/api/auth/login", { email, password }),
  register: (email: string, password: string) =>
    request<Me>("POST", "/api/auth/register", { email, password }),
  logout: () => request<void>("POST", "/api/auth/logout"),

  children: () => request<Child[]>("GET", "/api/children"),
  child: (id: string) => request<Child>("GET", `/api/children/${id}`),
  createChild: (child: ChildRequest) => request<Child>("POST", "/api/children", child),
  updateChild: (id: string, child: ChildRequest) =>
    request<Child>("PUT", `/api/children/${id}`, child),
  deleteChild: (id: string) => request<void>("DELETE", `/api/children/${id}`),
  today: (id: string) => request<Today>("GET", `/api/children/${id}/today`),
  extraTime: (id: string, minutes: number) =>
    request<void>("POST", `/api/children/${id}/extra-time`, { minutes }),
  pairingCode: (id: string) => request<PairingCode>("POST", `/api/children/${id}/pairing-codes`),

  devices: (childId: string) => request<Device[]>("GET", `/api/devices?childId=${childId}`),
  updateDevice: (id: string, name: string, offlineAllowanceOverrideMinutes: number | null) =>
    request<void>("PUT", `/api/devices/${id}`, { name, offlineAllowanceOverrideMinutes }),
  unpairDevice: (id: string) => request<void>("DELETE", `/api/devices/${id}`),

  apps: () => request<App[]>("GET", "/api/apps"),
  createApp: (name: string) => request<App>("POST", "/api/apps", { name }),
  deleteApp: (id: string) => request<void>("DELETE", `/api/apps/${id}`),
  addIdentifier: (appId: string, kind: MatcherKind, value: string) =>
    request<Identifier>("POST", `/api/apps/${appId}/identifiers`, { kind, value }),
  removeIdentifier: (appId: string, identifierId: string) =>
    request<void>("DELETE", `/api/apps/${appId}/identifiers/${identifierId}`),
  unassigned: () => request<UnassignedIdentifier[]>("GET", "/api/apps/unassigned"),
};
