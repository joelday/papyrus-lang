const path = require('path');

module.exports.createConfig = (tsConfigFile) => ({
    testEnvironment: 'node',
    transform: {
        '^.+\\.tsx?$': 'ts-jest',
    },
    moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node'],
    testRegex: '__tests__/.*\\.(ts|js)x?$',
    coverageDirectory: 'coverage',
    collectCoverageFrom: ['src/**/*.{ts,tsx,js,jsx}', '!src/**/*.d.ts'],
    globals: {
        'ts-jest': {
            tsConfigFile: tsConfigFile,
        },
    },
});
