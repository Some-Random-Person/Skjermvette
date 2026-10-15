# Parent dashboard (`web/`)

Loaded when working in `web/`. General rules are in the root `CLAUDE.md`.

## Stack
- React + Vite + TypeScript, with React Router for pages. Plain CSS in `src/index.css` with CSS variables. No CSS framework.
- The production build (`npm run build`) goes into `src/ScreenTime.Server/wwwroot` (gitignored), and the server serves it.
- Development: `npm run dev` (port 5173) forwards `/api` and `/hubs` to the server on port 5131, so the auth cookie stays same-origin.
- Pages: sign in / register, confirm email, forgot and reset password (the last three are linked from emails), children overview, child (Today / Rules / Devices tabs), add child, apps (catalog, identifiers, "not recognised yet"), settings (name, email, password, appearance, delete account).
- **npm packages (approved):** `react`, `react-dom`, `react-router`, plus dev tooling: `vite`, `@vitejs/plugin-react`, `typescript`, `@types/*`, `eslint` 9, `@eslint/js`, `typescript-eslint`, `eslint-plugin-react-hooks`, `prettier`. Anything else: ask first.

## Rules
- **All UI strings** live in `src/i18n/en.json` and are read with `t(key, params)`. Don't hard-code text in components.
- **Colours:** only the palette variables in `index.css`: `--bg-dark`, `--bg`, `--bg-light`, `--text`, `--text-muted`, `--highlight`, `--border`, `--border-muted`, `--primary`, `--secondary`, `--danger`, `--warning`, `--success`, `--info`.
  - Don't add raw colours. Transparent black for shadows and the dialog backdrop is the only exception.
  - **Dark is the default** (`:root`); light is `body.light`. Each colour has an `hsl` fallback followed by `oklch` (the user's palette, 2026-10-14).
  - Usage: page `--bg`, cards `--bg-light`, top bar `--bg-dark`, dividers and card borders `--border-muted`, inputs `--border`, family-made apps `--secondary`.
- **Theme switch:** Device (default, follows the system) / Light / Dark, saved in `localStorage` (key `theme`; nothing stored for Device).
  - Code in `src/theme.ts` and `ThemeSwitcher`, shown on Settings → Appearance and on the sign-in page.
  - A small inline script in `index.html` applies the saved theme before the page draws. **Keep it in step with `theme.ts`.**
- **Confirmations** use `useConfirm()` (the in-page dialog), never the browser's `confirm()`.
- **Picking an app** from a list uses `AppPicker` (search with suggestions), not a long `<select>`.
- Explanations parents need, such as the offline allowance, are written in plain language, with an example.

## Data
- `src/api/client.ts` is a fetch wrapper; its errors carry the ProblemDetails message, and a 401 sends the user to sign-in.
- `src/api/types.ts` mirrors the server's Contracts. Update both together.
- `useLoad(load, key, pollMs?)` loads data and can poll. The Today, children and devices views refresh every 15 s.
- `useAction()` gives a form busy / error / done state. The signed-in parent comes from `useAuth()` (with `setMe` after account changes).

## Checks
- Lint and format: ESLint 9 flat config, and Prettier (double quotes, trailing commas, 2-space indent, width 100).
- Run **`npm run check`** (types, lint, formatting) before committing. `npm run format` fixes formatting.
- **No frontend tests** for now. Rely on type checks, lint, server tests and a manual click-through in the browser against the real server.
