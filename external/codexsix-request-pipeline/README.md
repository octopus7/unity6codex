# CodexSix Request Pipeline

This package is a learning-oriented skeleton for studying Unity package layout.

## Folder Intent

- `Runtime/`: Runtime-facing contracts and a dummy implementation.
- `Editor/`: Editor-only UI and package settings entry points.
- `Tests/`: Runtime and editor test assemblies.
- `Samples~/`: Importable sample content.
- `Documentation~/`: Package docs that stay with the package.

## Included Scope

- Request/response contracts
- Transport abstraction
- Serializer abstraction
- Auth abstraction
- Minimal settings object

The implementation is intentionally small so the package shape is easy to inspect.

