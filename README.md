Impostor Server Plugins

Build

- Install Visual Studio (2022 or later) with .NET SDK.
- Open any `.sln` inside the specific plugin folder and build.
- Use NuGet to install the `Impostor.Api` package for each plugin project.

Run

- After building, copy the generated plugin `.dll` from `bin/Release` (or `bin/Debug`) into your Impostor server's `plugins` directory.

AUAC (Anti-Cheat)

- To build the `AUAC` plugin, you need a modified version of Impostor available on my GitHub. Build AUAC against that modified Impostor to ensure compatibility.

Notes

- The `AmongSusTracker` folder is intentionally excluded from version control.
- Typical build artifacts (`bin`, `obj`, `.vs`) are ignored.

