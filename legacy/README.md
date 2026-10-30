# Legacy C# interface (unsupported)

This folder preserves the original, unmaintained C# interface that shipped with
`CladCSharp.dll` and the Xamarin sample project. It is **not** part of the
supported SDK, is not built by `Cozmo.sln`, and is kept only for reference.

The original interface cannot work with current Cozmo apps:

- `CladCSharp.dll` is a 32-bit (x86) .NET Framework 2.5 assembly (`32BITREQUIRED`),
  so it cannot be loaded by a 64-bit .NET runtime.
- Its CLAD protocol definition (CladVersion 3.0.1) uses 1-byte union tags and is
  missing messages and fields that the 3.6.x engine sends. See
  [`../docs/clad-audit.md`](../docs/clad-audit.md) for the byte-level audit.

The supported SDK is the .NET 10 library under `../src/Cozmo`, generated from
cozmoclad 3.6.6 and covered by the oracle test suite.
