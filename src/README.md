# Innovation (C# port)

C# + WinForms (.NET 10) port of the 2013 VB6 implementation of Carl Chudyk's *Innovation*.

Goal: preserve original behavior and AI while fixing known bugs and providing a
modern buildable codebase on Windows 11.

## Projects

- `Innovation.Core` — rules engine, card data, AI (no UI).
- `Innovation.Tests` — xUnit tests pinning behavior to the original.
- `Innovation.App` — WinForms UI (added in a later phase).

## Build

```
dotnet build
dotnet test
```
