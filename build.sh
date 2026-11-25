#!/usr/bin/env sh
# Builds TypeCommander with Mono (Linux/macOS). Output matches build.bat:
#   build/TypeCommander.exe and dist/TypeCommander-v<VERSION>-win.zip
set -e
cd "$(dirname "$0")"
VER=$(tr -d ' \r\n' < VERSION)
mkdir -p obj build dist
cat > obj/Version.cs <<CS
[assembly: System.Reflection.AssemblyVersion("$VER.0")]
[assembly: System.Reflection.AssemblyFileVersion("$VER.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("$VER")]
CS
mcs -langversion:5 -target:winexe -optimize+ -win32icon:src/app.ico \
  -out:build/TypeCommander.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll \
  src/TypeCommander.cs src/AssemblyInfo.cs obj/Version.cs
cp README.md CHANGELOG.md build/
rm -f "dist/TypeCommander-v$VER-win.zip"
(cd build && zip -q "../dist/TypeCommander-v$VER-win.zip" TypeCommander.exe README.md CHANGELOG.md)
echo "Built build/TypeCommander.exe (v$VER)"
echo "Packaged dist/TypeCommander-v$VER-win.zip"
