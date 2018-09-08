export type FnArgs<T> = T extends (...args: infer U) => any ? U : never;
export type FnReturn<T> = T extends (...args: any[]) => infer U ? U : never;
