# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `FluffyChain`: chains are collected from the bone hierarchy and simulated in
  `LateUpdate` — Verlet integration with a rigid-length constraint, so bones lag
  behind the animated pose and swing back to it.
- `FluffyChain.ResetToRestPose` and teleport detection, so moving a character a
  long way in one frame does not launch the chain.
- `FluffyProfile`: stiffness with a falloff curve along the chain, drag and
  gravity.
- Scene view gizmos for the chain, both in play mode and while editing.

### Changed

- Minimum supported version raised to Unity 6.

## [0.0.1] - 2026-08-15

### Added

- Initial package scaffolding: assembly definitions, empty runtime and editor
  stubs, test assemblies, documentation and samples folders.

[Unreleased]: https://github.com/uayten/fluffy-bones/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/uayten/fluffy-bones/releases/tag/v0.0.1
