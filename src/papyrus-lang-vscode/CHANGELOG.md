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
