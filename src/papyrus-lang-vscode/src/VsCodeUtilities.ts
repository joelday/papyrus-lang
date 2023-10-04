import { CancellationTokenSource } from 'vscode';
import { delayAsync } from './Utilities';

export async function waitWhile(
    func: () => Promise<boolean>,
    cancellationToken = new CancellationTokenSource().token,
    pollingFrequencyMs = 1000
) {
    while ((await func()) && !cancellationToken.isCancellationRequested) {
        await delayAsync(pollingFrequencyMs);
    }
}
