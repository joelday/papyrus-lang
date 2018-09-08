import { createDecorator } from 'decoration-ioc';

export interface Logger {
    info(message: string, ...meta: any[]);
    error(message: string, ...meta: any[]);
    warn(message: string, ...meta: any[]);
    debug(message: string, ...meta: any[]);
}

export class ConsoleLogger implements Logger {
    public info(message: string, ...meta: any[]) {
        console.info(this.formatMessage('info', message), ...meta);
    }

    public error(message: string, ...meta: any[]) {
        console.error(this.formatMessage('error', message), ...meta);
    }

    public warn(message: string, ...meta: any[]) {
        console.warn(this.formatMessage('warn', message), ...meta);
    }

    public debug(message: string, ...meta: any[]) {
        console.debug(this.formatMessage('debug', message), ...meta);
    }

    private formatMessage(level: string, message: string) {
        return `${level}: ${message}`;
    }
}

// tslint:disable-next-line:variable-name
export const ILogger = createDecorator<Logger>('logger');
