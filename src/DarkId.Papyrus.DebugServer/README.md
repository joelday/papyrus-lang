# 📜 Papyrus Debug Adapter

[![Discord](https://img.shields.io/discord/558746231665328139.svg?color=%23738ADB)](https://discord.gg/E4dWujQ)

Websocket based debug adapter protocol xSE plugin for live Papyrus debugging support. Debug adapter protocol clients connect via the proxy executable found here: https://github.com/joelday/papyrus-lang/tree/master/src/DarkId.Papyrus.DebugAdapterProxy

File any issues here: https://github.com/joelday/papyrus-lang/issues

### Building from Source

Build Requirements:

- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/community/)
- [vcpkg](https://vcpkg.io/en/getting-started.html)

Run Requirements:

- [Fallout 4 address library mod](https://www.nexusmods.com/fallout4/mods/47327)
- [Skyrim address library mod (this is split into two mods for AE and SE)](https://www.nexusmods.com/skyrimspecialedition/mods/32444)

Make sure you've initialized the submodules in this project, i.e. `git clone --recurse-submodules` when cloning.

1. Install Visual Studio 2022 with C++ support (i.e. "Desktop development with C++" option in the installer)
2. Install vcpkg, and run `vcpkg integrate install` to enable it system-wide.
3. Open the solution in VS2022.
4. IMPORTANT: Do not click "Build Solution"; Currently, Visual Studio handles the simulataneous vcpkg install poorly and will screw up one of the two builds.
5. Build each project individually by right clicking on the project and clicking "Build". This should auto-download and install the required vcpkg dependencies, and then build the project.

When using this with `papyrus-lang`, this goes in "<your mod folder>/Papyrus Debugger/Data/SKSE" folder.

Make sure you have the address library mods installed before running.

### Acknowledgements

https://github.com/Orvid/Champollion

https://github.com/Samsung/netcoredbg

Special thanks to everyone in the xSE, Modding Tools and Papyrus Language Tools Discord servers.
