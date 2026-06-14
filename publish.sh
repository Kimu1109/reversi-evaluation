#!/bin/bash

cd reversi-evaluation

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

cd bin/Publish

echo "ZIP win-x64"
zip -r win-x64.zip win-x64

echo "TAR.GZ linux-64"
tar -zcvf linux-64.tar.gz linux-x64

echo "ZIP win-x64-selfcontained"
zip -r win-x64-selfcontained.zip win-x64-selfcontained

echo "TAR.GZ linux-64-selfcontained"
tar -zcvf linux-64-selfcontained.tar.gz linux-x64-selfcontained
