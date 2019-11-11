import { commands } from "vscode";


export async function showWelcome(papyrusVersion: string, previousVersion: string): Promise<void> {
    if (previousVersion === undefined) {
        console.log("No previous version found. This is a first time install. Showing welcome message.");
        await commands.executeCommand('papyrus.showWelcome');
    }
}