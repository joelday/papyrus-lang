import * as colorizer from './colorizer';
import * as lexer from './lexer';
import { ColorizerOptions as co } from './types';


export function colorizeJson(json: string, options?: co): string {
  return colorizer.colorize(lexer.getTokens(json, options), options);
};

export type ColorizerOptions = co;
export default colorizeJson;