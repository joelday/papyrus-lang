# 1.3.0-alpha.1
### Additions
* Added `papyrus.currentGame` configuration setting
* Added error logging to Papyrus Compiler output view
* Added Papyrus Compiler version info to Skyrim/Skyrim Special Edition Compilation
* Language Server and UI now refresh when settings are changed via VSCode Settings pages
### Changes
* Refactored PapyrusCompiler, PapyrusConfig, and PapyrusExtension a bit.
    * Papyrus Compiler output view is no longer const
    * Moved PapyrusConfigManager to PapyrusConfig from PapyrusExtension
    * Removed unneeded extension.ts
* Fixed a bug that caused the status bar buttons to either always be visible, or never be
* Fixed a bug that caused the compiler mode status bar button to do nothing
* Updated internal ppj file destination to VSCode extension managed folder
* Updated VSCode requirement to latest
# 1.2.0-alpha.3
### Changes
* Renamed Server request RegistryHandler to RegistryInstallPathHandler
# 1.2.0-alpha.2
### Additions
* Added a registry info function-thing to the server, becuase no good registry library exists.
# 1.2.0-alpha.1
### Additions
* Added Papyrus Compiler commands for Fallout 4, Skyrim, and Skyrim Special Edition
* Added Papyrus Project creation command for Fallout 4
* Added compile.asm, compile.mode, and SkyrimLE/SkyrimSE configuration settings
* Added game, compiler mode, and compiler assembly mode buttons to the status bar
* Added @types/ini, ini, and xml-writer node package dependencies
* Added PapyrusCompiler, PapyrusConfig, and PapyrusExtension classes
* PapyrusConfig manages reading the VSCode extension settings, and the CreationKit Ini file settings
* PapyrusCompiler takes the values read by PapyrusConfig and uses them to generate ppj files, command line arguments, and it handles PapyrusCompiler.exe interop, compiling files and showing the compiler output in VSCode
* PapyrusExtension manages certain VSCode frontend code, and connects to the Config and Compiler classes to control their backends
### Changes
* Removed the Papyrus Compiler path configuration setting, it is now read from the Creaiton Kit ini files instead
* Fixed a bug that caused the language server to repeatedly crash when loading a .psc file that wasn't in a workspace
* Fixed a bug that caused the inactive decoration to not appear on files that were already focused when VSCode starts
### To Do
* A lot of error checking
* Almost all of the error checking
* There's almost no error checking
* Error reporting
* There's not much error reporting either
* It either works or you don't know what it's not
* Fix ppj files also causing the language server to freak out
