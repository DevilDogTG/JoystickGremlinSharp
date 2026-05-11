# Driver patch list

These patches are applied **in numeric order** by `..\build.ps1` on top of the
upstream `BrunnerInnovation/vJoy` source clone (default tag: `v2.2.2`).

Patches are unified-diff format (`git diff`/`git format-patch` compatible).

| Patch | Purpose |
|---|---|
| `01-rename-service.patch` | Rename Windows service / class GUID / driver image from `vjoy` to `jgswheel` so the fork co-installs cleanly alongside stock vJoy. |
| `02-per-device-vidpid.patch` | Add registry reads for `Parameters\DeviceNN\VendorId` (DWORD) and `ProductId` (DWORD) — fall back to the new defaults (G29 = `0x046D`/`0xC24F`) when not set. Patches `vJoyGetDeviceAttributes` in `driver/sys/hid.c`. |
| `03-wheel-descriptor.patch` | Replace the generic-controller HID report descriptor with a Logitech-G29-compatible wheel descriptor: Usage Page 0x01, Usage 0x04, with Steering / Accelerator / Brake / Clutch axes + buttons + a D-Pad hat. |
| `04-renamed-interface-dll.patch` | Build user-mode wrapper as `JgsWheelInterface.dll` (same vJoy ABI exports) so existing FFB code resolves the new module. |

> **Status**: The patch files are not yet authored. They will be produced when
> the WDK build environment is provisioned and the upstream layout is verified.
> The `build.ps1` script gracefully reports an empty patch set as a warning so
> a "byte-identical-to-upstream" smoke build is possible for environment
> validation before patching begins.
