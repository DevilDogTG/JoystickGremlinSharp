# JGS Wheel Driver

The **JGS Wheel** backend (`jgs-wheel`) is a forked vJoy driver designed to
present the virtual device to games as a recognised racing wheel
(Logitech G29/G920, Thrustmaster T300RS/TMX) by overriding both the USB
VID/PID and the HID report descriptor that stock vJoy hardcodes.

> **Status**: the C# host code (interop, backend, FFB source) ships with
> JoystickGremlinSharp and degrades gracefully (`BackendStatus.NotInstalled`)
> when the driver is absent. The driver itself must be built and installed
> separately from `installer/wheel-driver/` until a signed binary is shipped.

## Why a fork?

Stock `vjoy.sys` defines `VENDOR_N_ID = 0x1234` / `PRODUCT_N_ID = 0xBEAD` as
compile-time constants in `inc/public.h`. Registry overrides have **no
effect** because the driver never reads them. Likewise the HID report
descriptor is baked in as a `joystick` device, not a wheel — so
HID-usage-detection games (Assetto Corsa, iRacing, AMS2, Project CARS) cannot
see steering / pedals as wheel axes, and VID/PID-whitelist games (Forza
Horizon 4/5, The Crew 2, NFS) reject the device entirely.

The fork addresses both problems:

1. `vJoyGetDeviceAttributes` reads VID/PID per-device from
   `HKLM\SYSTEM\CurrentControlSet\Services\jgswheel\Parameters\DeviceNN\VendorID`
   / `ProductID` (falling back to G29 defaults).
2. The static report descriptor is replaced with a wheel descriptor declaring
   Generic Desktop usages 0xC8 (Steering) / 0xC4 (Accelerator) / 0xC5 (Brake)
   / 0xC6 (Clutch) plus 32 buttons and one hat.
3. Service / class GUIDs are renamed (`jgswheel.sys`, `\\.\jgswheel`) so the
   driver coexists with stock vJoy.

## Building

Prerequisites: Windows 11 + Visual Studio Build Tools 2022 + WDK 10.

```powershell
cd installer/wheel-driver
.\build.ps1            # clones BrunnerInnovation/vJoy@v2.2.2, applies patches
.\build.ps1 -TestSign  # also produces a self-signed test certificate
```

Output: `out/jgswheel.sys`, `out/jgswheel.inf`, `out/JgsWheelInterface.dll`.

## Test-signing (for development)

The fork is **not EV-signed**. To load it, enable Windows test-signing:

```powershell
bcdedit /set testsigning on
shutdown /r /t 0
```

A "Test Mode" watermark appears on the desktop. Disable with
`bcdedit /set testsigning off`.

## EV-sign upgrade path

For public release the driver must be EV-signed and submitted to the Windows
Hardware Dev Center for attestation signing. See
<https://learn.microsoft.com/windows-hardware/drivers/install/cross-certificates-for-kernel-mode-code-signing>.
