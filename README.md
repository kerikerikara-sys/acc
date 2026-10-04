# AC Noxxer

Local scanner for consented screenshares. It looks on **this PC** for traces of FiveM cheats, DMA hardware,
anti-forensics and related browser / Discord activity. Checks run locally. Sign-in sends the account credentials,
license key and a device identifier to the configured server; after a scan, the findings are also submitted there.
Findings can include process names, file paths and matched text.

## Run
`ACNoxxer.exe` asks for administrator rights (needed for Prefetch, BAM and event-log checks). Without them it
still runs, marked LIMITED.

## What it checks
| Module | Looks at |
|---|---|
| System integrity | Secure Boot, Fast Boot, test-signing, cleared event logs, Prefetch state |
| Memory integrity | Windows Memory Integrity (HVCI): warning when disabled |
| DMA hardware | Xilinx / FTDI FT601 / KMBox devices in the PnP tree |
| Drivers | Known mapper-abused drivers, drivers in user folders |
| Processes | Names, window titles, unusual DLLs in FiveM/GTA processes |
| Network connections | Established TCP connections owned by processes that matched existing scan rules |
| Memory regions | Bounded, read-only signature scan of executable private memory in FiveM/GTA processes |
| Execution traces | Programs executed recently and then deleted (BAM), UserAssist, MuiCache, AppCompat, BAM, Prefetch, Recent, Jump lists, Timeline, PowerShell history |
| Recycle Bin | Deleted file names and dates, recently modified or emptied bin |
| File system | File and folder names across the user profile and drives |
| File contents | Strings inside exe/dll/sys (ASCII + UTF-16) and text/lua/config files |
| Browsers | Chrome, Edge, Brave, Vivaldi, Opera, Opera GX, Firefox: history, searches, sessions, favicons, cache |
| Discord | Desktop cache, local storage, IndexedDB (gzip bodies are inflated; author + message extracted) |
| FiveM | Logs, crash reports, plugins folder, GTA V folder ASI/hooks |

## Look (background and title)
Put `noxer_bg.jpg` (or `.png`) next to the exe to use your own background photo; the window adds a dark overlay.
Put your title text on the first line of `noxer_title.txt` (default: `NOXER`).

## Custom keywords
Create `noxxer_keywords.txt` next to the exe, one keyword per line.
`H:` prefix = high, `L:` = low, none = medium, `!` = whole word only, `#` = comment.

## Rebuild
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```
Uses the .NET Framework compiler that ships with Windows; no SDK needed.

## UI preview
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1 -Preview
```
Builds `ACNoxxer-preview.exe`, which opens the consent and ready screens without elevation, network checks or scan execution.

## Limits
A match is an indicator, not proof. Read each item in context. Brotli-compressed Discord data and live
process memory are not inspected. Cleared history leaves gaps that a scan cannot fill.
