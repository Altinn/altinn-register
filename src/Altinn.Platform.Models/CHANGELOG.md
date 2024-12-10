# Changelog

## [2.0.0](https://github.com/Altinn/altinn-register/compare/Altinn.Platform.Models-1.6.1...Altinn.Platform.Models-v2.0.0) (2024-12-10)


### ⚠ BREAKING CHANGES

* This change enables nullable reference types in Altinn.Platform.Models. All reference types are set as nullable by default and is likely to cause consumers of this library to get compiler warnings where they previously did not. Eventually, some of these properties will be set as non-nullable. We do not consider this a breaking change.
* This change makes all lists in Altinn.Platform.Models `IReadOnlyList<T>` instead of `List<T>`. This is to enable us to change the implementation of these lists in the future without breaking consumers of the library. The list properties are still writable, so the entire list can be replaced if for some reason that is needed.
* The `LanguageType` property in `ProfileSettingPreference` is marked obsolete. It is a set-only property which just proxies to the `Language` property and is only present for historical reasons. It will eventually be removed.
* Altinn.Platform.Models now targets .net 8.0. This means that consumers of this library must also target .net 8.0 or later.

### Features

* enable nullable reference types in Altinn.Platform.Models ([fefaa3d](https://github.com/Altinn/altinn-register/commit/fefaa3d774a0f139c6563fb611a426403d239056))
* make lists in Altinn.Platform.Models `IReadOnlyList&lt;T&gt;` ([fefaa3d](https://github.com/Altinn/altinn-register/commit/fefaa3d774a0f139c6563fb611a426403d239056))
* make most types record ([fefaa3d](https://github.com/Altinn/altinn-register/commit/fefaa3d774a0f139c6563fb611a426403d239056))


### Miscellaneous Chores

* make Altinn.Platform.Models target .net 8.0 ([fefaa3d](https://github.com/Altinn/altinn-register/commit/fefaa3d774a0f139c6563fb611a426403d239056))
* mark `LanguageType` in `ProfileSettingPreference` obsolte ([fefaa3d](https://github.com/Altinn/altinn-register/commit/fefaa3d774a0f139c6563fb611a426403d239056))
