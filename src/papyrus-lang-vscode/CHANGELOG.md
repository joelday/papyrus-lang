## [2.6.3](https://github.com/joelday/papyrus-lang/compare/v2.6.2...v2.6.3) (2019-06-04)


### Bug Fixes

* Papyrus debugging support on Windows 7. ([dacda50](https://github.com/joelday/papyrus-lang/commit/dacda50))

## [2.6.2](https://github.com/joelday/papyrus-lang/compare/v2.6.1...v2.6.2) (2019-06-03)


### Bug Fixes

* Default port for debug adapter proxy was wrong. ([694f46d](https://github.com/joelday/papyrus-lang/commit/694f46d))

## [2.6.1](https://github.com/joelday/papyrus-lang/compare/v2.6.0...v2.6.1) (2019-06-03)


### Bug Fixes

* Prevent error message when dismissing pre-debug checks. ([09b0d34](https://github.com/joelday/papyrus-lang/commit/09b0d34))

# [2.6.0](https://github.com/joelday/papyrus-lang/compare/v2.5.2...v2.6.0) (2019-06-03)


### Features

* Fallout 4 debugging support! ([16d7f2f](https://github.com/joelday/papyrus-lang/commit/16d7f2f))

## [2.5.2](https://github.com/joelday/papyrus-lang/compare/v2.5.1...v2.5.2) (2019-04-16)


### Bug Fixes

* now properly parses Papyrus ini properties with quoted values ([6f50fbb](https://github.com/joelday/papyrus-lang/commit/6f50fbb))

## [2.5.1](https://github.com/joelday/papyrus-lang/compare/v2.5.0...v2.5.1) (2019-04-16)


### Bug Fixes

* prevent error from attempting to resolve compiler path when install path is null. ([c2de135](https://github.com/joelday/papyrus-lang/commit/c2de135))

# [2.5.0](https://github.com/joelday/papyrus-lang/compare/v2.4.0...v2.5.0) (2019-03-30)


### Features

* autocomplete for remote event handlers ([b37e354](https://github.com/joelday/papyrus-lang/commit/b37e354))

# [2.4.0](https://github.com/joelday/papyrus-lang/compare/v2.3.0...v2.4.0) (2019-03-29)


### Bug Fixes

* remove duplicate state names from extended scripts in autocomplete ([7cc78e1](https://github.com/joelday/papyrus-lang/commit/7cc78e1))


### Features

* context menu command to open creationkit.com and search for the current hovered/selected text ([03418b3](https://github.com/joelday/papyrus-lang/commit/03418b3))

# [2.3.0](https://github.com/joelday/papyrus-lang/compare/v2.2.0...v2.3.0) (2019-03-29)


### Features

* autocompletion for struct members when calling FindStruct and RFindStruct ([5fa45d9](https://github.com/joelday/papyrus-lang/commit/5fa45d9))

# [2.2.0](https://github.com/joelday/papyrus-lang/compare/v2.1.0...v2.2.0) (2019-03-29)


### Bug Fixes

* exclude event definitions and remote event handlers from autocomplete. ([4a1171c](https://github.com/joelday/papyrus-lang/commit/4a1171c))


### Features

* RemoteEvent and GoToState autocomplete suggestions ([6a23778](https://github.com/joelday/papyrus-lang/commit/6a23778))

# [2.1.0](https://github.com/joelday/papyrus-lang/compare/v2.0.12...v2.1.0) (2019-03-29)


### Features

* release ([25f7ca7](https://github.com/joelday/papyrus-lang/commit/25f7ca7))

## [2.0.12](https://github.com/joelday/papyrus-lang/compare/v2.0.11...v2.0.12) (2019-03-28)


### Bug Fixes

* autocomplete and error refinements ([3488a3d](https://github.com/joelday/papyrus-lang/commit/3488a3d))

## [2.0.11](https://github.com/joelday/papyrus-lang/compare/v2.0.10...v2.0.11) (2019-03-27)


### Bug Fixes

* corrected references to the sCompilerFolder ini setting. ([c99cfcc](https://github.com/joelday/papyrus-lang/commit/c99cfcc))

## [2.0.10](https://github.com/joelday/papyrus-lang/compare/v2.0.9...v2.0.10) (2019-03-25)


### Bug Fixes

* README.md urls ([fc3f648](https://github.com/joelday/papyrus-lang/commit/fc3f648))

## [2.0.9](https://github.com/joelday/papyrus-lang/compare/v2.0.8...v2.0.9) (2019-03-25)


### Bug Fixes

* null/undefined check for command line args builder ([#29](https://github.com/joelday/papyrus-lang/issues/29)) ([#32](https://github.com/joelday/papyrus-lang/issues/32)) ([7fbbb8e](https://github.com/joelday/papyrus-lang/commit/7fbbb8e))

## [2.0.8](https://github.com/joelday/papyrus-lang/compare/v2.0.7...v2.0.8) (2019-03-24)


### Bug Fixes

* sr debugging ([7cfdf7b](https://github.com/joelday/papyrus-lang/commit/7cfdf7b))



## [2.0.7](https://github.com/joelday/papyrus-lang/compare/v2.0.6...v2.0.7) (2019-03-24)


### Bug Fixes

* adding additional debug output ([#30](https://github.com/joelday/papyrus-lang/issues/30)) ([44d9113](https://github.com/joelday/papyrus-lang/commit/44d9113))



## [2.0.6](https://github.com/joelday/papyrus-lang/compare/v2.0.5...v2.0.6) (2019-03-24)


### Bug Fixes

* reduce output severity ([3e5c574](https://github.com/joelday/papyrus-lang/commit/3e5c574))



## [2.0.5](https://github.com/joelday/papyrus-lang/compare/v2.0.4...v2.0.5) (2019-03-24)


### Bug Fixes

* resolving local global functions ([#28](https://github.com/joelday/papyrus-lang/issues/28)) ([bef52d5](https://github.com/joelday/papyrus-lang/commit/bef52d5))



## [2.0.4](https://github.com/joelday/papyrus-lang/compare/v2.0.3...v2.0.4) (2019-03-23)


### Bug Fixes

* reversing imports for correct precedence ([#25](https://github.com/joelday/papyrus-lang/issues/25)) ([2c4136f](https://github.com/joelday/papyrus-lang/commit/2c4136f))



## [2.0.3](https://github.com/joelday/papyrus-lang/compare/v2.0.2...v2.0.3) (2019-03-23)


### Bug Fixes

* group properties were not being resolved as members ([#22](https://github.com/joelday/papyrus-lang/issues/22)) ([207c403](https://github.com/joelday/papyrus-lang/commit/207c403))



## [2.0.2](https://github.com/joelday/papyrus-lang/compare/d38adbf...v2.0.2) (2019-03-22)

* Initial 2.0 release
