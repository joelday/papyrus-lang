import { createDecorator } from 'decoration-ioc';
import { ExtensionContext } from 'vscode';

export const IExtensionContext = createDecorator<ExtensionContext>('extensionContext');
