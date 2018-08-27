#!/bin/bash

pushd ./ > /dev/null

echo Pulling Fallout 4 base scripts...

mkdir papyrus/FO4Scripts
cd papyrus/FO4Scripts

curl -L -o Base.zip --silent https://www.dropbox.com/s/xw646rnme3o30pu/Base.zip?dl=1
unzip -f -q Base.zip -d Base

popd > /dev/null
