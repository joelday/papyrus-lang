import { SemVer } from "semver";
import { commands } from "vscode";

const updateMessageSemver = new SemVer("3.0.1");

export async function showWelcome(papyrusVersion: string, previousVersion: string | undefined): Promise<void> {
    if (!previousVersion) {
        console.log("No previous version found. This is a first time install. Showing welcome message.");
        await commands.executeCommand('papyrus.showWelcome');
        return;
    }

    const previous = new SemVer(previousVersion);
    const current = new SemVer(papyrusVersion);

    if (previous.compare(updateMessageSemver) <= 0 && current.compare(updateMessageSemver) >= 0) {
        console.log("Previous version was less than or equal to 3.0.1. Showing welcome message.");
        await commands.executeCommand('papyrus.showWelcome');
    }
}