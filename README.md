# Locklight

A portable Windows lock screen and Spotlight image browser by **Lawrence Shafer**, [shaferlabs.com](https://shaferlabs.com).

Browse locally cached photographs, preview full-resolution images, filter by orientation, and save one image or the entire current view as JPEGs.

## Run

Download the portable ZIP from Releases, extract it, and open `Locklight.exe`. No installer or administrator access is required. Designed for Windows 10 and 11 with the Windows .NET Framework 4.x component enabled.

The executable is unsigned. Windows Smart App Control or organization policies may block it. Successful execution on one PC does not guarantee execution on others.

## Current image detection

For picture lock screens, Locklight queries Windows and the per-user Creative registry path. For Spotlight, it opens the latest accessible Spotlight logon record because the Windows API can return a stock wallpaper. The footer explicitly reports when the exact active image cannot be confirmed. Only images still cached on the PC are available. Use Refresh or F5 after Windows changes images.

## Controls

- Select a thumbnail to preview an image.
- Choose All images, Landscape, or Portrait.
- Save image or Ctrl+S exports the selected image at its original dimensions as JPEG.
- Save all exports the filtered view; existing export filenames are skipped.
- Refresh or F5 rescans the local cache.

The application does not change the lock screen or Windows security settings, and makes no network requests.

## Build

Open Windows PowerShell in this repository and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The build uses the Windows .NET Framework C# compiler. No NuGet dependencies or .NET SDK are needed. The output is `Locklight.exe`. The current build uses the original compiler options; `app.manifest` is retained for reference but is not supplied to the compiler.

An optional local smoke test requires cached images:

```powershell
Start-Process .\Locklight.exe -ArgumentList '--self-test .\test-results' -Wait
```

It writes a JPEG and a report containing local image paths. Do not publish those test files.

## Signing

For a trusted certificate installed in the Windows personal certificate store, install the Windows SDK Signing Tools and run:

```powershell
.\Sign-Release.ps1 -CertificateThumbprint YOUR_CERTIFICATE_THUMBPRINT
```

See `SIGNING.txt`. No signing credentials are included in this repository.

## License

Copyright (C) 2026 Lawrence Shafer — https://shaferlabs.com

Licensed under **GNU GPL version 3 or later**. See `COPYING` and `NOTICE.txt`. Distribute the corresponding source and GPL notices with the application. Windows and Spotlight photographs retain their respective copyrights; they are not included in this repository.
