/** "device" follows the operating system's light/dark setting. */
export type ThemeChoice = "device" | "light" | "dark";

// Same key and logic as the early script in index.html, which applies the theme before the page draws.
const storageKey = "theme";
const prefersLight = window.matchMedia("(prefers-color-scheme: light)");

export function savedTheme(): ThemeChoice {
  try {
    const value = localStorage.getItem(storageKey);
    return value === "light" || value === "dark" ? value : "device";
  } catch {
    // Storage can be blocked (private windows, strict settings); fall back to the device setting.
    return "device";
  }
}

export function saveTheme(choice: ThemeChoice) {
  try {
    if (choice === "device") localStorage.removeItem(storageKey);
    else localStorage.setItem(storageKey, choice);
  } catch {
    // Not saved, but still applied for this visit.
  }
  applyTheme(choice);
}

export function applyTheme(choice: ThemeChoice) {
  const light = choice === "light" || (choice === "device" && prefersLight.matches);
  document.body.classList.toggle("light", light);
}

/** Re-applies "device" when the operating system switches between light and dark. */
export function followDeviceTheme(getChoice: () => ThemeChoice): () => void {
  const onChange = () => {
    if (getChoice() === "device") applyTheme("device");
  };
  prefersLight.addEventListener("change", onChange);
  return () => prefersLight.removeEventListener("change", onChange);
}
