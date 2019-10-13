## Table of Contents
1. [About the Project](#about-the-project)
1. [Questions and Help](#questions-and-help)
1. [Getting Started](#getting-started)
1. [Useful Resources](#useful-resources)

# About the Project

This is an Extension for [VSCode](https://code.visualstudio.com/) (Visual Studio Code), a cross-platform text editor, to support Bethesda's Papyrus scripting language. It is written primarily in TypeScript and C#.

Write mods for Fallout 4 and Skyrim with the aid of modern code editing features such as code completion, jump to definition, hover, symbol search and live updated diagnostics.

[![](https://vsmarketplacebadge.apphb.com/version-short/joelday.papyrus-lang-vscode.svg)](https://marketplace.visualstudio.com/items?itemName=joelday.papyrus-lang-vscode)

Uses the **[Papyrus Debug Adapter](https://github.com/joelday/papyrus-debug-server) xSE plugin** for live debugging.

# Questions and Help

Do you have questions or need help? Please come visit the....
[Papyrus Language Tools Discord
![Discord](https://img.shields.io/discord/558746231665328139.svg?color=%23738ADB)](https://discord.gg/E4dWujQ)

# Getting Started

## Building

First, you will need Windows with the following installed on your system:
- VSCode [Download and install](https://code.visualstudio.com/)
- Node.js [Download and install](https://nodejs.org/)
- Git for Windows [Download and install](https://git-scm.com/download/win)
- .NET Core 3.0 [Download and install](https://dotnet.microsoft.com/download/dotnet-core)

Also, this will be much easier if you use a **Powershell** console because the main build script is written in PS.

### 1
First, clone the repository using git:
```
git clone https://github.com/joelday/papyrus-lang.git
cd papyrus-lang
```
### 2
Run the build script with default targets:
```
.\build.ps1
```
In the same directory, run this to update the script extender debug plugins (which are provided by a separate repo):
```
.\build.ps1 -Script build.cake -Target update-debug-plugin
```
### 3
Run VSCode to open the papyrus-lang as a folder. If this is still your current directory then just run:
```
code .
```
Hit **Ctrl-Shift-D** to open the Debug panel. At the top select **Launch (Build Extension Only)**.

Hit **F5** to build and launch the extension with debugging. After a little while you will see another VSCode window open. This is the Extension Development Host version of VSCode running the extension that was just built. Any changes you made to the code would be reflected in the debug/test install of the extension running in this window.

# Useful Resources

- [The Typescript Programming Language Documentation](https://www.typescriptlang.org/docs/home.html)
- [VSCode Extension Anatomy](https://code.visualstudio.com/api/get-started/extension-anatomy)
- [Learn RxJS](https://www.learnrxjs.io/) (Reactive eXtensions for Javascript)
- [JavaScript Promises](https://javascript.info/async)
- The [Pro Git](https://git-scm.com/book/en/v2/) book