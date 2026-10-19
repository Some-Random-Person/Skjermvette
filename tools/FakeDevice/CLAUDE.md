# FakeDevice

Loaded when working in `tools/FakeDevice/`. A console app that pairs like a real device and reports made-up usage, for testing across devices without real agents. Usage is in the README ("Fake devices").

- Commands: `pair`, `use`, `live`, `state`, `watch`, `list`, `forget`. Arguments are parsed by hand, with no command-line package.
- Uses the shared device API types from `Skjermvette.Core.DeviceApi` and the approved SignalR client.
- Saved tokens live in `%LOCALAPPDATA%\Skjermvette\FakeDevice`, one JSON file per device name.
  - Names are unique on the computer (across children, ignoring case). `pair` refuses an existing name before using up the code, and saving never overwrites (#16).
  - The server itself allows the same device name for different children, or even the same child.
- `use` reports usage after the fact, so it can push an app over its limit. A real agent blocks at the limit.
- Errors from the server are shown as plain messages (`UsageException`), without stack traces.
