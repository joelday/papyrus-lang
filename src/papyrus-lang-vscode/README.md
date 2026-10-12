# Papyrus

Language support for Papyrus, the scripting language of Skyrim, Fallout 4 and Starfield.

The extension bundles a single native `Papyrus.LanguageServer` for your platform. It discovers `.ppj` project files in the workspace and provides diagnostics, completion, hover, go to definition, references, rename, symbols, semantic highlighting, folding, formatting and PEX disassembly for every project it finds. The Creation Kit's compiler is not used and nothing needs configuring; you still need the Creation Kit installed for the base game's script sources, which your project pulls in through `<Imports>`.

## Settings

| Setting | Purpose |
| --- | --- |
| `papyrus.server.path` | Use a `Papyrus.LanguageServer` executable of your own instead of the bundled one. |

## Commands

| Command | Purpose |
| --- | --- |
| Papyrus: Show Output | Open the language server's output channel. |
| Papyrus: View Assembly | Show the PEX disassembly of the current script. |

## Links

- [Source and issues](https://github.com/joelday/papyrus-lang)
- [User manual](https://github.com/joelday/papyrus-lang/wiki)
- [Discord](https://discord.gg/E4dWujQ)
