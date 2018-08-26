import { Scanner } from '../common/Scanner';
import { Token } from './Token';
import { TokenKind } from './TokenKind';

export class TokenScanner extends Scanner<Token> {
    constructor(iterator: Iterator<Token>) {
        super(iterator, () => {
            return new Token(
                TokenKind.Unknown,
                '',
                this.current
                    ? {
                          start: this.current.value.range.end,
                          end: this.current.value.range.end,
                      }
                    : { start: 0, end: 0 },
                this.current ? this.current.value : null
            );
        });
    }

    public skipTrivia() {
        this.skipWhile((t) => t.isTrivia);
    }

    public nextNonTrivia() {
        this.skipTrivia();
        return this.next();
    }

    public nextNonTriviaMultiline(includeDocComment: boolean = false) {
        let hasSeenNewLineTrivia = false;

        this.skipWhile((t) => {
            if (t.kind === TokenKind.NewLineTrivia) {
                hasSeenNewLineTrivia = true;
            }

            if (includeDocComment && !hasSeenNewLineTrivia && t.isDocComment) {
                return false;
            }

            return t.isTrivia || t.isEndOfLogicalLine;
        });

        return this.next();
    }

    public peekNonTrivia() {
        return this.doLookahead(() => this.nextNonTrivia());
    }
}
