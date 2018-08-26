#!/bin/bash

if [ -z "$CI" ]; then
    echo "Error: Publish script can only run in a CI environment."
    exit 1
fi

set -e

echo Publishing packages...

git add .
git reset --hard
git checkout master

git config credential.helper "store --file=.git/credentials"
echo "https://${GH_TOKEN}:@github.com" > .git/credentials

echo "//registry.npmjs.org/:_authToken=$NPM_TOKEN" > ~/.npmrc

lerna publish patch --yes

echo Publishing Visual Studio Code extension package...
pushd ./ > /dev/null
cd packages/papyrus-lang-vscode

../../node_modules/.bin/lerna exec --scope papyrus-lang-vscode npm install

vsce package
vsce publish --pat $VSCE_TOKEN

popd > /dev/null
