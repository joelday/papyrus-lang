//@ts-check

const path = require('path');
const ForkTsCheckerWebpackPlugin = require('fork-ts-checker-webpack-plugin');

/**@type {import('webpack').Configuration}*/
const config = {
    target: 'node',

    entry: './src/PapyrusExtension.ts',
    output: {
        path: path.resolve(__dirname, 'dist'),
        filename: 'PapyrusExtension.js',
        libraryTarget: 'commonjs2',
        devtoolModuleFilenameTemplate: '../[resource-path]',
    },
    devtool: 'source-map',
    externals: {
        vscode: 'commonjs vscode',
        'ps-list': 'ps-list',
    },
    resolve: {
        extensions: ['.ts', '.js'],
    },
    module: {
        rules: [
            {
                test: /\.ts$/,
                exclude: /node_modules/,
                use: [
                    {
                        loader: 'ts-loader',
                        options: {
                            compilerOptions: {
                                module: 'es6',
                            },
                            transpileOnly: true,
                        },
                    },
                ],
            },
        ],
    },
    plugins: [new ForkTsCheckerWebpackPlugin()],
};

module.exports = config;
