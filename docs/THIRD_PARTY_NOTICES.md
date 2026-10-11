# Third-party notices

KeyBridge source is distributed under the repository's `LICENSE` (GNU General Public License version 3). Review the repository license and distribution obligations before commercial release. Original generated package icons are part of KeyBridge source and do not contain third-party marks.

A self-contained Windows x64 publish includes Microsoft .NET and Windows Desktop runtime files. Microsoft .NET is open source under the MIT license with additional component notices. Packaging reads the published runtime configuration and copies the exact restored runtime packs' licenses and component notices as `DOTNET_RUNTIME_LICENSE.txt`, `DOTNET_RUNTIME_THIRD_PARTY_NOTICES.txt` and `DOTNET_DESKTOP_LICENSE.txt`; the script fails if they are missing. The selected runtime versions are recorded in `KeyBridge.runtimeconfig.json` and may differ from the SDK's own runtime.

The Windows-targeted project also ships `Microsoft.Windows.SDK.NET.dll` and `WinRT.Runtime.dll` from its Microsoft Windows SDK .NET targeting pack. Its official NuGet metadata identifies Microsoft's [Windows SDK license](https://aka.ms/WinSDKLicenseURL). The unmodified RTF distributed by Microsoft is preserved at `docs/third-party/WindowsSDKLicense.rtf` and copied to `WINDOWS_SDK_LICENSE.rtf` in the package. Inspect the exact targeting-pack metadata and notices when updating it.

Primary sources:

- [.NET runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT)
- [.NET runtime third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT)
- [WPF license](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT)
- [Windows Forms license](https://github.com/dotnet/winforms/blob/main/LICENSE.TXT)

These links do not replace the version-specific license files bundled with the selected runtime. A release cannot pass the notices gate until that distribution has been inspected. Development/build SDK tools are tooling rather than separately claimed app runtime requirements. If dependency packages are added, update this document with exact package IDs, versions, SPDX/license information and redistributable notices.
