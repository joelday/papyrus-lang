import semanticRelease from "semantic-release";

async function getVersionFromSemanticRelease() {
    return await semanticRelease({
        dryRun: true,
        ci: false,
    }, {
        stdout: process.stderr
    });
}

async function getBuildVersion() {
    const result = await getVersionFromSemanticRelease();

    if (result === false || result.nextRelease.channel === "prerelease") {
        // returns a version number based on the current date and time, formatted as YYYY.MMDD.HHMM
        const date = new Date();
        const year = date.getFullYear();
        const month = date.getMonth() + 1;
        const day = date.getDate();
        const hours = date.getHours();
        const minutes = date.getMinutes();

        return `${year
            }.${month.toString().padStart(2, "0")
            }${day.toString().padStart(2, "0")
            }.${hours.toString().padStart(2, "0")
            }${minutes.toString().padStart(2, "0")
            }`;
    }

    return result.nextRelease.version;
}

export async function main() {
    process.stdout.write(await getBuildVersion());
}

main().catch((e) => {
    console.error(e);
    process.exit(1);
});