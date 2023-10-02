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
            plugins: ['@typescript-eslint', 'unused-imports'],
            extends: ['plugin:@typescript-eslint/recommended'],
            rules: {
                '@typescript-eslint/no-unused-vars': 'off',
                'unused-imports/no-unused-imports': 'error',
                'unused-imports/no-unused-vars': [
                    'warn',
                    { vars: 'all', varsIgnorePattern: '^_', args: 'after-used', argsIgnorePattern: '^_' },
                ],
                'prettier/prettier': 'warn',
            },
        },
    ],
};
