#!/bin/bash

if [ -z "$CI" ]; then
    echo "Error: Publish script can only run in a CI environment."
    exit 1
fi

set -e

echo Publishing packages...

git checkout master
git add .
git reset --hard

lerna publish prerelease --preid preview

echo Publishing Visual Studio Code extension package...
pushd ./ > /dev/null
cd packages/papyrus-lang-vscode

rm -rf node_modules

npm install
vsce package
vsce publish

popd > /dev/null
