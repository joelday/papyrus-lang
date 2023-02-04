import semanticRelease from "semantic-release";

async function getVersionFromSemanticRelease() {
    return await semanticRelease({
        dryRun: true,
        ci: false,
    }, {
        stdout: process.stderr
    });
}

export async function main() {
    const result = await getVersionFromSemanticRelease();
    if (result) {
        process.stdout.write(result.nextRelease.version);
    }
}

main().catch((e) => {
    console.error(e);
    process.exit(1);
});