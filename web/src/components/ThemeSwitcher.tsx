import { useEffect, useRef, useState } from "react";
import { t } from "../i18n/t";
import { followDeviceTheme, saveTheme, savedTheme, type ThemeChoice } from "../theme";

/** Device / Light / Dark. The choice is remembered in this browser. */
export function ThemeSwitcher() {
  const [choice, setChoice] = useState<ThemeChoice>(savedTheme);
  const choiceRef = useRef(choice);

  useEffect(() => {
    choiceRef.current = choice;
  }, [choice]);

  useEffect(() => followDeviceTheme(() => choiceRef.current), []);

  return (
    <label className="theme-switch small">
      {t("theme.label")}
      <select
        value={choice}
        onChange={(e) => {
          const next = e.target.value as ThemeChoice;
          setChoice(next);
          saveTheme(next);
        }}
      >
        <option value="device">{t("theme.device")}</option>
        <option value="light">{t("theme.light")}</option>
        <option value="dark">{t("theme.dark")}</option>
      </select>
    </label>
  );
}
