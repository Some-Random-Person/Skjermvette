// Mirrors the server's DTOs in src/ScreenTime.Server/Contracts. Enums are serialized as strings.

export type AppRuleKind = "Limited" | "Exempt";
export type MatcherKind = "Process" | "Package" | "Bundle" | "Domain" | "IosToken";
export type DevicePlatform = "Windows" | "Android" | "Ios" | "MacOs" | "Linux";

export const matcherKinds: MatcherKind[] = ["Process", "Domain", "Package", "Bundle", "IosToken"];

export interface Me {
  email: string;
  name: string | null;
  familyId: string;
}

/** The second sign-in step. `RecoveryCode` only works when the authenticator app is set up. */
export type TwoFactorMethod = "App" | "Email" | "RecoveryCode";

/** After the password: signed in (`me`, on a remembered browser), or the second step that's needed. */
export interface LoginResponse {
  me: Me | null;
  secondStep: TwoFactorMethod | null;
}

/** The second step is the authenticator app when `appEnabled`, otherwise an emailed code. */
export interface TwoFactor {
  appEnabled: boolean;
  recoveryCodesLeft: number;
}

/** The key for an authenticator app: as text, as an `otpauth://` link, and as a QR code (SVG). */
export interface AuthenticatorSetup {
  key: string;
  uri: string;
  qrCodeSvg: string;
}

export interface RecoveryCodes {
  codes: string[];
}

/** What deleting the account would remove. Children and devices only go when `isLastParent`. */
export interface AccountDeletion {
  isLastParent: boolean;
  children: number;
  devices: number;
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
