# Radial Follow Smoothing
An automated version of AbstractQbit's Radial Follow Smoothing using Mixmg's RFS Calculator formula.

You should not use this at the same time as the default RFS plugin!

# Installing
Download AutoRadialFollow.zip from Releases and open/extract it

Open Plugin Manager > File > Install plugin... > Select RadialFollow.dll

Optionally (not required): Open plugins directory... > RadialFollow > Drag included metadata.json into folder

# Manually Building
Run ``build.ps1`` inside PowerShell (.NET6 SDK Required)

If missing required packages, install inside RadialFollow directory:
```sh
dotnet add package OpenTabletDriver --version 0.6.1
dotnet add package OpenTabletDriver.Plugin --version 0.6.1
```
