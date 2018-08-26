import { Diagnostics, DiagnosticsError } from '../src/Diagnostics';

import { StringBuilder } from '../src/common/StringBuilder';

describe('Error', () => {
    it('correctly formats a debug output string', () => {
        const error = new DiagnosticsError(
            new Diagnostics(
                'fileName',
                // tslint:disable-next-line:max-line-length
                '012345678901234567890123456789012345678901234567890123456789012345678901234567890re is a long line,\r\nlong enough to test some of the padding logic herrrrrrrrrrre\r\ncode'
            ),
            'Yep, this is an error.',
            { start: 80, end: 90 }
        );

        const sb = new StringBuilder();
        sb.appendLine('Error at 80,90 in fileName:');
        sb.appendLine(
            // tslint:disable-next-line:max-line-length
            '   789012345678901234567890123456789012345678901234567890re is a long line,  long enough to test some of the padding lo'
        );
        sb.append(
            // tslint:disable-next-line:max-line-length
            '                                                        ^~~~~~~~~~ Yep, this is an error.'
        );

        expect(error.toString()).toBe(sb.toString());
    });
});
