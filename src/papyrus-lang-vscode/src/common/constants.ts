// Constants for use elsewhere in the code.
// We shoud slowly start moving some things in here.

export const extensionId = 'papyrus-lang-vscode';
export const extensionQualifiedId = `joelday.${extensionId}`;

export enum GlobalState {
    PapyrusVersion = 'papyrusVersion',
}
export const PDSModName = 'Papyrus Debug Extension';
export const AddressLibraryF4SEModName = 'Address Library for F4SE Plugins';
export const AddressLibrarySKSEAEModName = 'Address Library for SKSE Plugins (AE)';
export const AddressLibrarySKSEModName = 'Address Library for SKSE Plugins';

// TODO: Move these elsewhere
export type AddressLibraryName =
    | typeof AddressLibraryF4SEModName
    | typeof AddressLibrarySKSEAEModName
    | typeof AddressLibrarySKSEModName;
export enum AddressLibAssetSuffix {
    SkyrimSE = 'SkyrimSE',
    SkyrimAE = 'SkyrimAE',
    Fallout4 = 'Fallout4',
}
