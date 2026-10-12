import strings from "./en.json";

export type StringKey = keyof typeof strings;

/** Looks up a UI string and fills in `{name}` placeholders. */
export function t(key: StringKey, params: Record<string, string | number> = {}): string {
  return strings[key].replace(/\{(\w+)\}/g, (match, name: string) =>
    name in params ? String(params[name]) : match,
  );
}
