import { interfaces } from 'inversify';
import { ExtensionContext } from 'vscode';

export const IExtensionContext: interfaces.ServiceIdentifier<ExtensionContext> = Symbol('extensionContext');
