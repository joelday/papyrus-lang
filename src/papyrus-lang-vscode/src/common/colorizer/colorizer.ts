import * as chalk_d from 'chalk'
import get from 'lodash.get';
import { ColorizerOptions, Token } from './types';

interface ColorMap {
  [key: string]: string;
}

const defaultColors: ColorMap = {
  BRACE: 'gray',
  BRACKET: 'gray',
  COLON: 'gray',
  COMMA: 'gray',
  STRING_KEY: 'magenta',
  STRING_LITERAL: 'yellow',
  NUMBER_LITERAL: 'green',
  BOOLEAN_LITERAL: 'cyan',
  NULL_LITERAL: 'white'
};

const nonForcedChalk = new chalk_d.default.constructor();
const forcedChalk = new chalk_d.default.constructor({ enabled: true, level: 2 });
export function colorize(tokens: Token[], options?: ColorizerOptions): string {
  const colors = options?.colors || {};

  return tokens.reduce((acc, token) => {
    const chalk : chalk_d.Chalk = options?.forceColor ? forcedChalk : nonForcedChalk;
    const colorKey = colors[token.type] || defaultColors[token.type];
    const colorFn = colorKey && colorKey[0] === '#' ? chalk.hex(colorKey) : get(chalk, colorKey);

    return acc + (colorFn ? colorFn(token.value) : token.value);
  }, '');
}