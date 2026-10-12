# Welcome to Papyrus 4

Papyrus 4 is a new extension from the ground up, built on a new Papyrus compiler and a new language server. We're excited for you to try it, and we want to hear what you think.

## What's new

- **A real compiler.** The language server compiles Papyrus itself. Diagnostics are precise, references and rename work across scripts, and the editor understands your code the way the game does.
- **One server for the whole workspace.** Every `.ppj` in your workspace is discovered automatically. No game paths, no compiler settings, nothing to configure.
- **Linux and macOS.** The server ships as a native binary for Windows, Linux, and macOS on both Intel and Apple Silicon.
- **More editor features.** Semantic highlighting, workspace symbols, call and type hierarchy, inlay hints, code lens, on-type formatting, and **Papyrus: View Assembly** to see the PEX your script compiles to.
- **Fast.** Native code, no runtime to spin up, and a server that stays responsive on large projects.

Projects are `.ppj` files, and your project's `<Imports>` tell the server where the base game's scripts are, so keep the Creation Kit installed.

## Not here yet

Some 3.x features haven't made it into 4.0. Some are coming back, and some are waiting to hear whether anyone needs them:

- Live debugging
- Build tasks from the editor
- The project explorer panel
- Generate Project
- Working without a `.ppj` file
- The bundled Pyro build tool (Pyro itself still works fine alongside the extension)

## Tell us what you think

This is a big release, and your feedback shapes what comes next. What broke, what you miss, what you love: [join the Discord](https://discord.gg/E4dWujQ) or [open an issue](https://github.com/joelday/papyrus-lang/issues).

If you need to go back to 3.x for now, open the Extensions view, find **Papyrus**, and use **Switch to Release Version** (or the gear menu's **Install Another Version...** and pick 3.2.0). Your settings will be waiting for you.

You can reopen this page any time with **Papyrus: Show Welcome**.
