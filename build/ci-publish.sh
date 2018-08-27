#!/bin/bash

# if [ -z "$CI" ]; then
#     echo "Error: Publish script can only run in a CI environment."
#     exit 1
# fi

set -e

echo Publishing packages...

git add .
git reset --hard
git checkout master

git config credential.helper "store --file=.git/credentials"
echo "https://${GH_TOKEN}:@github.com" > .git/credentials

echo "//registry.npmjs.org/:_authToken=$NPM_TOKEN" > ~/.npmrc

lerna publish minor --yes $LERNA_PUBLISH_OPTIONS

pushd ./ > /dev/null
cd packages/papyrus-lang-vscode

rm -rf node_modules
rm package-lock.json
npm install --no-optional

published_vscode_ext_version=$(vsce show joelday.papyrus-lang-vscode --json | jq '.versions[0].version')
current_vscode_ext_version=$(cat ./package.json | jq '.version')

echo "Publishing Visual Studio Code extension package..."
if [ $published_vscode_ext_version != $current_vscode_ext_version ]; then
    vsce package
    vsce publish --pat $VSCE_TOKEN
else
    echo "Current published version is up to date."
fi

popd > /dev/null
