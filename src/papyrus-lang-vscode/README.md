# Papyrus

Language support for Papyrus, the scripting language of Skyrim, Fallout 4 and Starfield.

The extension bundles a native `Papyrus.LanguageServer` for your platform (Windows, Linux and macOS). It finds the `.ppj` project files in your workspace and provides, for every project it finds:

- diagnostics as you type
- completion, hover and signature help
- go to definition, find references and rename across scripts
- document and workspace symbols, call and type hierarchy
- semantic highlighting, folding, inlay hints and code lens
- formatting, including on-type formatting
- **Papyrus: View Assembly**, the PEX disassembly of the current script

The extension has its own compiler and does not use the Creation Kit's.

## Getting started

1. Open the folder that holds your mod's scripts.
2. Add a `.ppj` project file if you don't have one. A minimal project:

   ```xml
   <PapyrusProject Game="sse" Flags="TESV_Papyrus_Flags.flg" Output="Scripts">
     <Imports>
       <Import>Source/Scripts</Import>
       <Import>C:/Steam/steamapps/common/Skyrim Special Edition/Data/Source/Scripts</Import>
     </Imports>
     <Folders>
       <Folder>Source/Scripts</Folder>
     </Folders>
   </PapyrusProject>
   ```

   `Game` is one of `sse`, `tesv`, `fo4` or `starfield`. Relative paths are resolved from the `.ppj` file's folder.
3. List the base game's script sources and your dependencies under `<Imports>`, either as local folders (the Creation Kit installs the base game's sources) or as remote imports that Pyro downloads. The extension never looks for the game on its own, so anything your scripts extend or call has to be reachable through an import.

Several `.ppj` files in one workspace are fine; each is loaded as its own project.

## Pyro

The extension reads `.ppj` files with [Pyro](https://github.com/fireundubh/pyro) itself, so variables, imports, folders and scripts mean exactly what they mean when Pyro builds the project. Problems Pyro finds in a `.ppj` are shown in the output channel with their line and column.

Remote imports, like `https://github.com/MrOctopus/nl_online/tree/main/sse/source`, use your Pyro install's download cache and its access token. If Pyro has a GitHub token, missing remotes are downloaded into Pyro's cache when the project loads. Otherwise, build the project with Pyro once, and the extension picks the downloads up without a restart.

The extension uses the Pyro in `papyrus.pyro.path`, or the first one on the PATH. If a project has remote imports and there's no Pyro, it offers to open the Pyro releases page or to locate an existing copy.

## Settings

| Setting | Purpose |
| --- | --- |
| `papyrus.server.path` | Use a `Papyrus.LanguageServer` executable of your own instead of the bundled one. |
| `papyrus.pyro.path` | The Pyro whose download cache and token serve remote imports. Empty means the first Pyro on the PATH. |

## Commands

| Command | Purpose |
| --- | --- |
| Papyrus: View Assembly | Show the PEX disassembly of the current script. |
| Papyrus: Show Output Channel | Open the language server's output. |
| Papyrus: Locate Pyro | Pick the Pyro executable and save it to `papyrus.pyro.path`. |
| Papyrus: Show Welcome | Reopen the 4.0 welcome page. |

## Links

- [Source and issues](https://github.com/joelday/papyrus-lang)
- [Discord](https://discord.gg/E4dWujQ)
