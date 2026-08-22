N8's IP Scanner - Fast Scan + InNasc XML + GitHub Updates

This is the next revision after SettingsGearCentered.

What's new

Faster scans

The scanner now uses controlled parallel scanning instead of scanning one IP at a time.





Default timeout lowered from 300 ms to 200 ms



Web port timeout is capped tighter



Hostname lookup now times out quickly instead of slowing the whole scan



/24 scans should feel dramatically faster



Larger CIDR scans still warn before starting

InNasc XML export

The old CSV export button is now:

Export XML

It saves:

InNasc-IPScan-Import.xml

The XML maps discovered scan results into InNasc/AV Matrix-style equipment fields:





Description



Manufacturer



Hostname



Serial



Firmware



PrimaryIP



SecondaryIPs



MACs



Subnet



Gateway



Username



Password



Notes



NetworkInterfaces

Fields the scanner cannot know, such as serial, firmware, username, and password, are exported as blank values.

GitHub software updates

Settings now includes:

Software Updates → Check GitHub

It checks the latest GitHub release from:

IamN8Wright/N8s-IPScanner

To make updates work, publish a GitHub release with a newer version tag, such as:

v2.4.0

Then attach the new EXE or ZIP as a release asset.

Existing clean layout retained





Custom centered Settings gear



Centered Selected NIC Settings buttons



Light mode default



Dark mode option



Update OUI List inside Settings



Loopback/disconnected adapter visibility toggles



CIDR/full-subnet scanning



Passive discovery / advanced capture



HTTP/HTTPS status detection



Double-click web rows to open a device web UI



Single self-contained EXE build



Build

Double-click:

Build-SingleExe.cmd

Output:

dist-single\N8s-IPScanner.exe



Manual build

From the N8sIPScanner project folder:

dotnet restore "N8sIPScanner.csproj" -r win-x64 --source https://api.nuget.org/v3/index.json

dotnet publish "N8sIPScanner.csproj" -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "..\dist-single"

