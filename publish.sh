#!/bin/bash

echo "WELCOME TO PUBLISH SCRIPT!"
echo "win-x64, win-x64-selfcontained, linux-x64 and linux-x64-selfcontained will be built."
echo "================================"

cd reversi-evaluation

echo "REMOVE Publish folder"
rm -r bin/Publish

echo "BUILD win-x64"

dotnet publish \
    -c Release \
    -r win-x64 \
    --self-contained false \
    -o bin/Publish/win-x64

echo "BUILD linux-x64"

dotnet publish \
    -c Release \
    -r linux-x64 \
    --self-contained false \
    -o bin/Publish/linux-x64

echo "BUILD win-x64-selfcontained"

dotnet publish \
    -c Release \
    -r win-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o bin/Publish/win-x64-selfcontained

echo "BUILD linux-x64-selfcontained"

dotnet publish \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o bin/Publish/linux-x64-selfcontained

echo "COPY LICENSE & README to win-x64"
cp ../LICENSE bin/Publish/win-x64
cp ../LICENSE.edax bin/Publish/win-x64
cp ../README.md bin/Publish/win-x64

echo "COPY LICENSE & README to linux-x64"
cp ../LICENSE bin/Publish/linux-x64
cp ../LICENSE.edax bin/Publish/linux-x64
cp ../README.md bin/Publish/linux-x64

echo "COPY LICENSE & README to win-x64-selfcontained"
cp ../LICENSE bin/Publish/win-x64-selfcontained
cp ../LICENSE.edax bin/Publish/win-x64-selfcontained
cp ../README.md bin/Publish/win-x64-selfcontained

echo "COPY LICENSE & README to linux-x64-selfcontained"
cp ../LICENSE bin/Publish/linux-x64-selfcontained
cp ../LICENSE.edax bin/Publish/linux-x64-selfcontained
cp ../README.md bin/Publish/linux-x64-selfcontained

cd bin/Publish

echo "ZIP win-x64"
zip -r win-x64.zip win-x64

echo "TAR.GZ linux-64"
tar -zcvf linux-64.tar.gz linux-x64

echo "ZIP win-x64-selfcontained"
zip -r win-x64-selfcontained.zip win-x64-selfcontained

echo "TAR.GZ linux-64-selfcontained"
tar -zcvf linux-64-selfcontained.tar.gz linux-x64-selfcontained

echo "FINISH!"
