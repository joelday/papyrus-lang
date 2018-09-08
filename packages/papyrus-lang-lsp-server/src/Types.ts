export type FnArgs<T> = T extends (...args: infer U) => any ? U : never;
