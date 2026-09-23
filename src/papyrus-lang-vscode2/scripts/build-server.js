// Publishes the NativeAOT Papyrus.LanguageServer for this machine into bin/<rid>/,
// where ServerPathResolver looks for the bundled server.
const { execFileSync } = require('child_process');
const path = require('path');

const rids = {
    'win32-x64': 'win-x64',
    'linux-x64': 'linux-x64',
    'darwin-x64': 'osx-x64',
    'darwin-arm64': 'osx-arm64',
};

const rid = rids[`${process.platform}-${process.arch}`];
if (!rid) {
    console.error(`No Papyrus.LanguageServer build for ${process.platform}-${process.arch}.`);
    process.exit(1);
}

const project = path.resolve(__dirname, '..', '..', 'Papyrus.LanguageServer');
const output = path.resolve(__dirname, '..', 'bin', rid);

// On macOS the AOT link step needs Apple's clang; a Homebrew clang earlier on PATH
// cannot find the SDK ("ld: library 'dl' not found").
const env = { ...process.env };
if (process.platform === 'darwin') {
    env.PATH = `/usr/bin:${env.PATH}`;
}

execFileSync('dotnet', ['publish', project, '-c', 'Release', '-r', rid, '-o', output], {
    stdio: 'inherit',
    env,
});
