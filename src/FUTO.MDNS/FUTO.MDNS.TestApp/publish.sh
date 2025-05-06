#!/bin/sh
dotnet publish -r osx-arm64 --self-contained true -p:PublishSingleFile=true -c Release
dotnet publish -r linux-x64 --self-contained true -p:PublishSingleFile=true -c Release