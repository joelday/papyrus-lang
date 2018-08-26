#!/bin/bash

set -e

echo Publishing packages...
lerna publish prerelease --preid preview

echo Publishing Visual Studio Code extension package...
pushd ./ > /dev/null
cd packages/papyrus-lang-vscode

rm -rf node_modules

npm install
vsce package
vsce publish

popd > /dev/null
