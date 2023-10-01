// Constants for use elsewhere in the code.
// We shoud slowly start moving some things in here.

export const extensionId = 'papyrus-lang-vscode';
export const extensionQualifiedId = `joelday.${extensionId}`;

export enum GlobalState {
    PapyrusVersion = 'papyrusVersion'
}
export const PDSModName = "Papyrus Debug Extension";
export const AddressLibraryF4SEModName = "Address Library for F4SE Plugins";
export const AddressLibrarySKSEAEModName = "Address Library for SKSE Plugins (AE)";
export const AddressLibrarySKSEModName = "Address Library for SKSE Plugins";

// TODO: Move these elsewhere
export type AddressLibraryName = typeof AddressLibraryF4SEModName | typeof AddressLibrarySKSEAEModName | typeof AddressLibrarySKSEModName;
export enum AddressLibAssetSuffix {
    SkyrimSE = 'SkyrimSE',
    SkyrimAE = 'SkyrimAE',
    Fallout4 = 'Fallout4',
}

export function getAsssetLibraryDLSuffix(addlibname: AddressLibraryName): AddressLibAssetSuffix {
    switch (addlibname) {
        case AddressLibrarySKSEModName:
            return AddressLibAssetSuffix.SkyrimSE;
        case AddressLibrarySKSEAEModName:
            return AddressLibAssetSuffix.SkyrimAE;
        case AddressLibraryF4SEModName:
            return AddressLibAssetSuffix.Fallout4
    }
}
export function getAddressLibNameFromAssetSuffix(suffix: AddressLibAssetSuffix): AddressLibraryName {
    switch (suffix) {
        case AddressLibAssetSuffix.SkyrimSE:
            return AddressLibrarySKSEModName;
        case AddressLibAssetSuffix.SkyrimAE:
            return AddressLibrarySKSEAEModName;
        case AddressLibAssetSuffix.Fallout4:
            return AddressLibraryF4SEModName;
    }
}
