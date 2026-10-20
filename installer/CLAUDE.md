# Windows agent: installer

Loaded when working in `installer/`. General rules are in the root `CLAUDE.md`; the Service it installs is described in `src/Skjermvette.Agent.Service/CLAUDE.md`.

## Shape
- WiX Toolset **v5** (`WixToolset.Sdk/5.0.2`, plus `UI` and `Util` extensions). Not v6+, which comes under the Open Source Maintenance Fee licence. `dotnet build installer -c Release` → `bin/Release/Skjermvette.Agent.msi`, x64, per machine.
- Not in `Skjermvette.slnx`, so `dotnet test` doesn't publish the agent twice.
- `PublishAgent` publishes both programs **self-contained**, always Release (a Debug Helper is a console program with a window): the Service into `obj/agent/`, the Helper into `obj/agent/Helper/`. Not one folder: the two runtimes' copies of a few files (`System.Drawing.dll`, `System.Diagnostics.EventLog.dll`, …) differ, and the second publish would overwrite the first's.
- `Package.wxs`: files (`<Files>` globs; `.pdb` and `appsettings.Development.json` left out), the service (`ServiceInstall` as LocalSystem, auto start; `util:ServiceConfig` restarts it after 5 s, three times a day), and the pairing actions. `PairDlg.wxs`: WixUI's standard dialogs with PairDlg between Welcome and Ready.
- `Version` (default 0.1.0) sets the MSI's and both programs' version. `MajorUpgrade` with same-version upgrades allowed, so a test build installs over the last one.

## Pairing and forgetting
- `SERVER` (default `https://`) and `PAIRCODE` (hidden from the log) come from PairDlg or the command line. A first install without `PAIRCODE` stops with `RequirePairCode`; upgrades skip the dialog and keep the pairing.
- `PairDevice` runs `Skjermvette.Agent.Service.exe pair` as SYSTEM after `InstallFiles` and before `StartServices`, so the service starts paired. If it fails the install rolls back. Error 1722 (an EXE custom action failed) is overridden with what to do, since it's the only EXE action that can fail the install.
- `ForgetDeviceRollback` comes after `PairDevice`, so it only forgets a pairing this install made, if a later step fails.
- `ForgetDevice` runs `forget` on uninstall, after the service stops, but not when an upgrade removes the old version.
- Not done yet (#71): the parent PIN for uninstalling (plan rule 8; any administrator can uninstall), unpairing on the server at uninstall, and code signing (SmartScreen warns).

## Checking a build
- Installing it starts the real agent, which closes programs: exempt the programs in use on the test child first (README → Windows agent), and Claude asks the user before every install.
- **Tried** (2026-10-20, silent `/qn` against the dev server): a wrong code fails with the 1722 message and leaves nothing behind; the right code installs, pairs and starts the service (LocalSystem, auto start, three restarts), the Helper runs in the user's session and the device shows as online; the code isn't in the MSI log; uninstalling runs `forget` and removes the service, the files and the Helper. Then by hand (2026-10-20, the user clicking): PairDlg (Next stays put until both fields are filled), upgrading 0.1.0 → 0.2.0 (no pairing page, one Apps entry, the same device keeps checking in) and uninstalling from Settings → Apps.
- Without installing, the MSI's tables can be read through the `WindowsInstaller.Installer` COM object (e.g. a short VBScript with `OpenDatabase` + `OpenView`).
