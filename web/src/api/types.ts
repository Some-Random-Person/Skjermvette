// Mirrors the server's DTOs in src/ScreenTime.Server/Contracts. Enums are serialized as strings.

export type AppRuleKind = "Limited" | "Exempt";
export type MatcherKind = "Process" | "Package" | "Bundle" | "Domain" | "IosToken";
export type DevicePlatform = "Windows" | "Android" | "Ios" | "MacOs" | "Linux";

export const matcherKinds: MatcherKind[] = ["Process", "Domain", "Package", "Bundle", "IosToken"];

export interface Me {
  email: string;
  familyId: string;
}

export interface AppRule {
  appId: string;
  kind: AppRuleKind;
  limitMinutes: number | null;
}

export interface ChildRequest {
  name: string;
  timeZoneId: string;
  resetHour: number;
  dailyTotalMinutes: number;
  offlineAllowanceMinutes: number;
  rules: AppRule[];
}

export interface Child extends ChildRequest {
  id: string;
}

export interface Identifier {
  id: string;
  kind: MatcherKind;
  value: string;
  isBuiltIn: boolean;
}

export interface App {
  id: string;
  name: string;
  isBuiltIn: boolean;
  identifiers: Identifier[];
}

export interface UnassignedIdentifier {
  kind: MatcherKind;
  rawIdentifier: string;
  minutesLast7Days: number;
  lastSeenUtc: string;
}

export interface Device {
  id: string;
  childId: string;
  name: string;
  platform: DevicePlatform;
  pairedAtUtc: string;
  lastSeenAtUtc: string | null;
  isOnline: boolean;
  offlineAllowanceOverrideMinutes: number | null;
  setAsideMinutes: number;
}

export interface PairingCode {
  code: string;
  expiresAtUtc: string;
}

export interface TodayApp {
  appId: string | null;
  name: string;
  kind: AppRuleKind | null;
  usedMinutes: number;
  limitMinutes: number | null;
  remainingMinutes: number | null;
}

export interface Today {
  childId: string;
  dayStart: string;
  dayEnd: string;
  totalLimitMinutes: number;
  extraMinutes: number;
  totalUsedMinutes: number;
  totalRemainingMinutes: number;
  apps: TodayApp[];
  devices: Device[];
}
