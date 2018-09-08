import { IInstantiationService } from 'decoration-ioc';
import { Connection } from 'vscode-languageserver';
import { ILanguageServerConnection } from './External';
import { FnArgs } from './Types';

export interface Handler<T extends keyof ConnectionFns> {
    readonly handleRequest: HandlerFn<T>;
}

export type HandlerCtor<T extends keyof ConnectionFns> = new (...args: any[]) => Handler<T>;

export type ConnectionFns = Record<keyof Connection, Function>;
export type HandlerFn<T extends keyof ConnectionFns> = FnArgs<Connection[T]>[0];
export type HandlerRegistration<T extends keyof ConnectionFns = keyof ConnectionFns> = [T, HandlerCtor<T>];

export class RequestHandlerService {
    private readonly _instances: Map<string, any> = new Map();
    constructor(
        registrations: HandlerRegistration[],
        @ILanguageServerConnection connection: Connection,
        @IInstantiationService instantiationService: IInstantiationService
    ) {
        for (const registration of registrations) {
            const handlerName = registration[0];
            const handlerCtor = registration[1];

            connection[handlerName as any]((...args: any[]) => {
                if (!this._instances.has(handlerName)) {
                    this._instances.set(handlerName, instantiationService.createInstance(handlerCtor));
                }

                return this._instances.get(handlerName).handleRequest(...args);
            });
        }
    }
}
