import { useId, useState, type KeyboardEvent } from "react";
import type { App } from "../api/types";
import { t } from "../i18n/t";

interface Props {
  apps: App[];
  /** Field label for screen readers, e.g. "Assign to an app". */
  label: string;
  /** Button text when the typed name matches an app. */
  pickLabel: (app: App) => string;
  onPick: (app: App) => Promise<void> | void;
  /** When given, a name that matches no app can be created; the button shows this text. */
  createLabel?: (name: string) => string;
  onCreate?: (name: string) => Promise<void>;
}

/**
 * A search field with the app names as suggestions, replacing long dropdowns. Typing an app's name (any case)
 * picks it; with onCreate, typing a new name offers to create that app. Not a <form>, so it can sit inside one.
 */
export function AppPicker({ apps, label, pickLabel, onPick, createLabel, onCreate }: Props) {
  const listId = useId();
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);

  const name = text.trim();
  const match = apps.find((a) => a.name.toLowerCase() === name.toLowerCase());
  const canCreate = !!onCreate && !!createLabel && name.length > 0 && name.length <= 100 && !match;

  const submit = async () => {
    if (busy || (!match && !canCreate)) return;
    setBusy(true);
    try {
      if (match) await onPick(match);
      else await onCreate!(name);
      setText("");
    } finally {
      setBusy(false);
    }
  };

  const onKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== "Enter") return;
    // Don't submit a surrounding form (e.g. the rules form).
    e.preventDefault();
    // Enter only picks an existing app. Creating one takes a click on the button, so a half-typed
    // search like "minec" can't accidentally create an app called "minec".
    if (match) void submit();
  };

  return (
    <div className="inline app-picker">
      <input
        type="search"
        list={listId}
        value={text}
        placeholder={t("apps.searchPlaceholder")}
        aria-label={label}
        className="grow"
        onChange={(e) => setText(e.target.value)}
        onKeyDown={onKeyDown}
      />
      <datalist id={listId}>
        {apps.map((app) => (
          <option key={app.id} value={app.name} />
        ))}
      </datalist>
      <button
        type="button"
        className="primary"
        disabled={busy || (!match && !canCreate)}
        onClick={() => void submit()}
      >
        {match ? pickLabel(match) : canCreate ? createLabel!(name) : label}
      </button>
    </div>
  );
}
