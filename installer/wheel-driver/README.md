# Wheel-driver build (vJoy fork)

Builds an unmodified-functionality copy of
[BrunnerInnovation/vJoy](https://github.com/BrunnerInnovation/vJoy) v2.2.2.0
from source so we have a reproducible local pipeline for future
patching/customisation work (e.g. VID/PID changes, wheel HID descriptor).

> **Status**: build pipeline works end-to-end. The current output is
> functionally identical to upstream vJoy — service name `vjoy`, VID `0x1234`,
> PID `0xBEAD`. No rename / VID-PID / descriptor patches are applied yet.

## Folder layout

```
installer/wheel-driver/
  README.md             ← this file
  build.ps1             ← clones upstream + applies patches + builds + (optional) test-signs
  patches/              ← unified-diff patches applied on top of upstream tag
    0001-add-pnplockdown.patch   (required for modern InfVerif)
  build/                ← (gitignored) clone of vJoy source
  out/                  ← (gitignored) build output
```

After `build.ps1 -TestSign` succeeds, `out/` contains:

| File | Purpose |
|---|---|
| `vJoy.sys`         | Function driver |
| `hidkmdf.sys`      | HID-KMDF mapper filter driver |
| `vJoy.inf`         | Driver INF |
| `vJoy.cat`         | Catalogue (signed) |
| `vJoyInterface.dll`| User-mode helper DLL (vJoy ABI) |
| `jgswheel-test.cer`| Self-signed test certificate (only with `-TestSign`) |

## Build prerequisites

1. **Visual Studio 2022 or 2026** with the **Desktop development with C++** workload
   - Includes `MSVC v143 x64` toolset
2. **Windows Driver Kit (WDK)** 10.0.22621 or later
   ([download](https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk))
3. **PowerShell 7+** and **git**
4. The `Inf2Cat.exe` tool (ships with the WDK SDK at
   `C:\Program Files (x86)\Windows Kits\10\bin\<sdk>\x86\`) must be on `$PATH`
   when running `-TestSign`. The build script does not auto-add it.

> **VS2026 specifics**: VS2026 splits MSBuild toolsets across `VC\v170\` (hosts
> the v143 sub-toolset) and `VC\v180\` (hosts `WindowsKernelModeDriver10.0`).
> `build.ps1` auto-detects both paths and orchestrates each project against
> the right `VCTargetsPath`. No manual config needed.

## Building

From a **VS Developer PowerShell** (so `msbuild` is on `$PATH`):

```powershell
cd installer\wheel-driver
.\build.ps1 -Configuration Release -TestSign
```

Useful overrides (rarely needed):

| Parameter | Default | Purpose |
|---|---|---|
| `-UpstreamRepo`   | `https://github.com/BrunnerInnovation/vJoy.git` | Source repo |
| `-UpstreamTag`    | `v2.2.2.0` | Pinned tag |
| `-Configuration`  | `Release` | `Release` or `Debug` |
| `-KmdfMajor` / `-KmdfMinor` | `1` / `15` | Override KMDF version (upstream pins to 1.9 which has been removed from current WDKs) |
| `-PlatformToolset` | _(unset)_ | Force a specific toolset |

To rebuild from a clean source tree:

```powershell
Remove-Item -Recurse -Force .\build\vjoy-src
.\build.ps1 -Configuration Release -TestSign
```

## Installing the test-signed driver (developer machines only)

> **Secure Boot must be OFF.** Test-signed drivers are blocked at the
> bootloader level when Secure Boot is on, regardless of `bcdedit /set
> testsigning on`. To re-enable Secure Boot later you would need a Microsoft
> attestation-signed driver — see *Production signing* below.

> **BitLocker warning.** Disabling Secure Boot will trip BitLocker's TPM
> integrity check on the next boot. Have your recovery key ready
> (`manage-bde -protectors -get C:`).

Run elevated (admin) PowerShell:

```powershell
cd installer\wheel-driver\out

# 1) Trust the test cert in BOTH stores
Import-Certificate -FilePath jgswheel-test.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath jgswheel-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher

# 2) Enable test-signing
bcdedit /set testsigning on

# 3) Reboot — confirm "Test Mode" watermark appears bottom-right of the desktop
Restart-Computer

# 4) After reboot, install the driver
pnputil /add-driver vJoy.inf /install
```

Verify install:

```powershell
pnputil /enum-drivers | Select-String -Pattern 'vjoy' -Context 0,5
Get-Service vjoy | Format-Table Name, Status, StartType
Get-PnpDevice -Class HIDClass | Where-Object FriendlyName -match 'vJoy' | Format-Table Status, FriendlyName
```

Expected: `vjoy` service registered; HID device shows `Status: OK` (not
`Error`/`CM_PROB_UNSIGNED_DRIVER`).

### Uninstalling

```powershell
pnputil /enum-drivers | Select-String -Pattern 'vjoy' -Context 0,5  # find the oem*.inf name
pnputil /delete-driver oem<NN>.inf /uninstall /force
bcdedit /set testsigning off                                        # then reboot
```

## Running JGS with the test-signed driver

JoystickGremlinSharp on the host loads `vJoyInterface.dll` from the installed
vJoy directory (or the copy bundled with this build). With the test-signed
driver active, the host sees an extra `vJoy Device` (VID `0x1234` / PID
`0xBEAD`) — DirectInput games on the same machine will see it too.

This means **the host machine itself becomes the test target** — Option 1
gives you full real-world testing of JGS + the wheel driver, but the cost is
having Secure Boot off. Option 2 below is an alternative if you don't want to
touch host security.

## Production signing (path to ship)

Test-signing is a dev-only workflow. To distribute the driver to users without
forcing them to disable Secure Boot:

1. Buy an **EV code-signing certificate** (DigiCert, Sectigo, SSL.com — ~$300/yr).
2. Re-sign `vJoy.sys`, `hidkmdf.sys`, and `vJoy.cat` with the EV cert.
3. Submit the package to **Microsoft Partner Center → Hardware Dashboard** for
   **Attestation Signing** (free, automated for HID drivers, ~24h turnaround).
4. The returned driver package installs on any Windows machine with Secure
   Boot ON and Test Mode OFF, no user action required beyond accepting a
   standard driver-install prompt.

WHQL/HLK certification is a heavier alternative (full hardware lab kit
testing) and is not required for HID drivers.

## Alternative: build & test entirely inside a VM

If you don't want to disable Secure Boot on your host:

1. Create a **Hyper-V Generation 1 VM** (Gen 1 has no Secure Boot at all; Gen 2
   does, and disabling it on Gen 2 still works but is more fiddly).
2. Install Windows 10/11 inside the VM.
3. Copy `installer\wheel-driver\out\` into the VM.
4. Run the install steps above inside the VM.

**Caveat**: the driver only enumerates inside the VM. JGS and games running on
the host cannot see the virtual wheel from the guest — there is no HID-bridge
across the VM boundary. Use this path only for smoke-testing the driver
itself (does it install, enumerate, accept FFB packets), not for real
gameplay testing.

## Why a custom build at all?

Stock vJoy is fine for "virtual gamepad" use cases. The reason this folder
exists is to keep the door open to building a wheel-emulating fork that
identifies as a known wheel VID/PID with a wheel HID descriptor — needed by
games (Forza, ACC, iRacing, etc.) that whitelist specific wheels.

A previous investigation concluded that doing this *and* shipping it requires
EV cert + Microsoft attestation signing (see *Production signing*), which is
outside the current scope of the project. The build pipeline here is the
prerequisite for that work; the actual VID/PID and descriptor patches are not
written yet.

## Patch authoring

Patches in `patches/` are applied in lexical order via `git apply
--whitespace=fix` after the upstream tag is checked out. To add a new patch:

```powershell
# After modifying files under build\vjoy-src\
cd build\vjoy-src
git diff > ..\..\patches\0002-my-change.patch
git checkout .   # discard the working-tree change; the patch now lives in patches/
```

Re-run `.\build.ps1` to verify the patch applies cleanly from scratch.
