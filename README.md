# N8s IP Scanner - Hostname Enrichment

This is the next revision after `SettingsGearCentered`.

## What's new

### Faster scans

The scanner now uses controlled parallel scanning instead of scanning one IP at a time.

- Default timeout lowered from `300 ms` to `200 ms`
- Web port timeout is capped tighter
- Hostname lookup now times out quickly instead of slowing the whole scan
- /24 scans should feel dramatically faster
- Larger CIDR scans still warn before starting

### InNasc XML export

The old CSV export button is now:

```text
Export XML
```

It saves:

```text
InNasc-IPScan-Import.xml
```

The XML maps discovered scan results into InNasc/AV Matrix-style equipment fields:

- Description
- Manufacturer
- Hostname
- Serial
- Firmware
- PrimaryIP
- SecondaryIPs
- MACs
- Subnet
- Gateway
- Username
- Password
- Notes
- NetworkInterfaces

Fields the scanner cannot know, such as serial, firmware, username, and password, are exported as blank values.

### GitHub software updates

Settings now includes:

```text
Software Updates → Check GitHub
```

It checks the latest GitHub release from:

```text
IamN8Wright/N8s-IPScanner
```

Release builds are now handled by GitHub Actions. The workflow reads the version from `N8sIPScanner/N8sIPScanner.csproj`, builds the self-contained Windows EXE and ZIP, creates the matching `vX.Y.Z` tag when it is new, and publishes the GitHub Release with those assets.

The workflow can also be started manually from **Actions → Build Windows EXE → Run workflow**. If a release tag is entered manually, it must match the project version.

### Hostname enrichment

This revision keeps the faster parallel scanner, but spends a small extra hostname budget on devices that are actually found online.

- Empty/offline IPs still scan quickly
- Live devices get up to about 1.2 seconds of hostname enrichment
- Reverse DNS and NetBIOS lookups run in parallel
- More Windows/AV devices should show hostnames without making the whole subnet crawl

## Existing clean layout retained

- Custom centered Settings gear
- Centered Selected NIC Settings buttons
- Light mode default
- Dark mode option
- Update OUI List inside Settings
- Loopback/disconnected adapter visibility toggles
- CIDR/full-subnet scanning
- Passive discovery / advanced capture
- HTTP/HTTPS status detection
- Double-click web rows to open a device web UI
- Single self-contained EXE build

## Build

Double-click:

```text
Build-SingleExe.cmd
```

Output:

```text
dist-single\N8s IP Scanner.exe
```

## Manual build

From the `N8s IP Scanner` project folder:

```cmd
dotnet restore "N8s IP Scanner.csproj" -r win-x64 --source https://api.nuget.org/v3/index.json

dotnet publish "N8s IP Scanner.csproj" -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o "..\dist-single"
```

## Name update

Visible product name updated to `N8s IP Scanner`.

## v2.3.3 Branding/About update

- Visible product name remains `N8s IP Scanner`.
- Logo and app icon updated to the new N8/InN8 Labs mark.
- Settings now includes a small InN8 Labs about/support area with `iamn8wright@gmail.com`.
- GitHub release/updater URL uses the real repo slug: `IamN8Wright/N8s-IPScanner`.
- No deep scan mode was added.
