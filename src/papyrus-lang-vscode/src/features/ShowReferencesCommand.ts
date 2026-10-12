import { commands, Disposable, Location, Position, Range, Uri } from 'vscode';

export const showReferencesCommandId = 'papyrus.showReferences';

interface LspPosition {
    line: number;
    character: number;
}

interface LspLocation {
    uri: string;
    range: { start: LspPosition; end: LspPosition };
}

interface ShowReferencesArguments {
    uri: string;
    position: LspPosition;
    locations: LspLocation[];
}

const toPosition = (p: LspPosition) => new Position(p.line, p.character);

/**
 * Target of the server's reference-count code lenses. Lens command arguments arrive as
 * plain JSON, while `editor.action.showReferences` needs real `Uri`/`Position`/`Location`s.
 */
export function registerShowReferencesCommand(): Disposable {
    return commands.registerCommand(showReferencesCommandId, (args: ShowReferencesArguments) =>
        commands.executeCommand(
            'editor.action.showReferences',
            Uri.parse(args.uri),
            toPosition(args.position),
            args.locations.map(
                (l) => new Location(Uri.parse(l.uri), new Range(toPosition(l.range.start), toPosition(l.range.end)))
            )
        )
    );
}
