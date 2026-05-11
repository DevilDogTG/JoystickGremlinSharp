# JGS Wheel Driver (`jgswheel.sys`)

Forked from [BrunnerInnovation/vJoy](https://github.com/BrunnerInnovation/vJoy) v2.2.x.
Adds **per-device VID/PID** and **wheel HID descriptor** support so games with
VID/PID-whitelist or HID-usage detection (Forza, ACC, iRacing, etc.) recognise
the virtual device as a Logitech G29 / Thrustmaster T300RS wheel.

## Why a fork is required

Stock `vjoy.sys` hardcodes:

- `VID_1234 / PID_BEAD` as compile-time constants (`VENDOR_N_ID` / `PRODUCT_N_ID`
  in `inc/public.h`)
- A generic-controller HID report descriptor in `driver/sys/hid.c`

Registry overrides under `HKLM\SYSTEM\CurrentControlSet\Services\vjoy\Parameters`
are silently ignored by the driver. Only recompiling can change these values.

## What this fork changes

| Area | Stock vJoy | JGS Wheel fork |
|---|---|---|
| Service name | `vjoy` | `jgswheel` |
| Class GUID | `{781EF630-72B2-11d2-B852-00C04FAD5101}` | new GUID |
| Default VID/PID | 0x1234 / 0xBEAD | 0x046D / 0xC24F (G29) |
| Per-device VID/PID | not supported | read from registry: `Parameters\DeviceNN\VendorId` (DWORD) / `ProductId` (DWORD) |
| HID descriptor | generic gamepad | racing wheel — Usage Page 0x01, Usage 0x04 (Joystick) + Steering / Accelerator / Brake / Clutch |
| Co-installation | conflicts | side-by-side with stock vJoy |

The user-mode interface DLL is renamed to `JgsWheelInterface.dll` and exports
the same vJoy ABI so existing FFB code can be reused.

## Folder layout

```
installer/wheel-driver/
  README.md           ← this file
  build.ps1           ← clones upstream + applies patches + builds with WDK
  patches/            ← unified-diff patches applied on top of upstream tag
    01-rename-service.patch
    02-per-device-vidpid.patch
    03-wheel-descriptor.patch
    04-renamed-interface-dll.patch
  out/                ← (gitignored) build output: jgswheel.sys, JgsWheelInterface.dll, jgswheel.cat
```

## Build prerequisites

1. **Visual Studio 2022 Build Tools** with `MSVC v143 x64` and `Windows SDK 10.0.22621+`
2. **Windows Driver Kit (WDK) 10.0.22621+** — https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk
3. **PowerShell 7+** and **git**
4. (Optional) **EWDK** can substitute for full WDK if you only need CLI builds

## Building

```powershell
cd installer\wheel-driver
.\build.ps1 -Configuration Release -TestSign
```

The script:
1. Clones `BrunnerInnovation/vJoy` at the pinned tag into `build\vjoy-src`
2. Applies all patches under `patches/` in numeric order
3. Builds `jgswheel.sys` and `JgsWheelInterface.dll` with `msbuild`
4. (If `-TestSign`) generates a self-signed dev cert in the user store and signs
   the `.sys` + `.cat` with `signtool`

Outputs land in `installer\wheel-driver\out\`.

## Installing the driver (developer machines only)

```powershell
# 1) Enable test-signing mode (one time, requires reboot)
bcdedit /set testsigning on

# 2) Install the self-signed cert into Trusted Root + TrustedPublisher
Import-Certificate -FilePath out\jgswheel-test.cer -CertStoreLocation Cert:\LocalMachine\Root
Import-Certificate -FilePath out\jgswheel-test.cer -CertStoreLocation Cert:\LocalMachine\TrustedPublisher

# 3) Install the driver via the JoystickGremlinSharp installer or:
pnputil /add-driver out\jgswheel.inf /install
```

## EV signing (production)

For a public release without test-signing, the `.sys` must be EV-signed and
attestation-signed by Microsoft (Hardware Dev Center). Steps documented in
`docs/jgs-wheel-driver.md`.

## Status

**This driver is currently a planned deliverable, not a built artefact.** The
patches and build script exist; building requires the WDK on a developer
machine. The C# managed-code (`JoystickGremlin.Interop.JgsWheel`) ships behind
a feature flag (`AppSettings.EnableJgsWheelBackend`, default `false`) and
gracefully reports `BackendStatus.NotInstalled` when the driver is absent.
