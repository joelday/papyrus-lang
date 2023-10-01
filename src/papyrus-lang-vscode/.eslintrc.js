/* eslint-env node */
module.exports = {
    root: true,
    parser: '@typescript-eslint/parser',
    env: {
        node: true,
    },
    extends: ['eslint:recommended', 'plugin:prettier/recommended'],
    overrides: [
        {
            files: ['*.ts'],
            plugins: ['@typescript-eslint'],
            extends: ['plugin:@typescript-eslint/recommended'],
            rules: {
                '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
            },
        },
    ],
};
