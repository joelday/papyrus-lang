export type TokenType =
  | 'BRACE'
  | 'BRACKET'
  | 'COLON'
  | 'COMMA'
  | 'STRING_KEY'
  | 'STRING_LITERAL'
  | 'NUMBER_LITERAL'
  | 'BOOLEAN_LITERAL'
  | 'NULL_LITERAL'
  | 'WHITESPACE';
export interface Token {
    type: TokenType;
    value: string;
}
export interface ColorizerOptions {
    readonly pretty?: boolean;
    readonly colors?: { readonly [token in TokenType]?: string };
    readonly forceColor?: boolean;
}