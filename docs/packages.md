# Packages

## Papyrus.Pex

[![NuGet](https://img.shields.io/nuget/vpre/Papyrus.Pex.svg)](https://www.nuget.org/packages/Papyrus.Pex)

Standalone library for Papyrus PEX 3.1–3.15: read, write, normalize, and reflect compiled scripts. No compiler dependency.

```xml
<PackageReference Include="Papyrus.Pex" Version="0.1.0" />
```

This is an unstable **0.x** package. There is no compatibility promise. Public surface may change without a major bump.

`dotnet pack` produces the `.nupkg`. CI uploads it as a workflow artifact. Publishing to nuget.org is a separate, manual step and is not run on pull requests.

| | |
|---|---|
| **PackageId** | `Papyrus.Pex` |
| **Version** | `0.1.0` (`VersionPrefix`; suffix optional) |
| **License** | MIT |
| **Repository** | https://github.com/joelday/papyrus-lang |

## Papyrus.CodeAnalysis

[![NuGet](https://img.shields.io/nuget/vpre/Papyrus.CodeAnalysis.svg)](https://www.nuget.org/packages/Papyrus.CodeAnalysis)

Compiler library for Papyrus: syntax, symbols, and emit. Depends on `Papyrus.Pex`. The syntax source generator used to build this library is not shipped; it may be a separate pack later.

```xml
<PackageReference Include="Papyrus.CodeAnalysis" Version="0.1.0" />
```

This is an unstable **0.x** package. There is no compatibility promise. Public surface may change without a major bump.

`dotnet pack` produces the `.nupkg`. CI uploads it as a workflow artifact. Publishing to nuget.org is a separate, manual step and is not run on pull requests.

| | |
|---|---|
| **PackageId** | `Papyrus.CodeAnalysis` |
| **Version** | `0.1.0` (`VersionPrefix`; suffix optional) |
| **License** | MIT |
| **Repository** | https://github.com/joelday/papyrus-lang |
