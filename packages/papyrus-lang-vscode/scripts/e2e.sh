#!/usr/bin/env bash

export CODE_TESTS_PATH="$(pwd)/lib/test"
export CODE_TESTS_WORKSPACE="$(pwd)/testFixture"

node "$(pwd)/client/node_modules/vscode/bin/test"
