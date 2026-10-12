# Changelog

Releases are listed on the [GitHub releases page](https://github.com/joelday/papyrus-lang/releases).

## 4.0.0

A ground-up rewrite. The extension now launches one native `Papyrus.LanguageServer` process for the whole workspace instead of one .NET Framework host per game, and that server compiles Papyrus itself instead of wrapping the Creation Kit compiler.

- Linux and macOS are supported.
- `.ppj` project files are discovered automatically; no Creation Kit paths or compiler settings.
- A one-time welcome (**Papyrus: Show Welcome**) explains what changed, what is missing and how to go back to 3.x.
- Live debugging is not yet available in this version. The xSE debug plugin is still built and published with each release and will be wired back into the extension in a later 4.x release.
