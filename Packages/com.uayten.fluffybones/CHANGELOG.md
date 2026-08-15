# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `FluffyBones`: the character-level component. Holds the chains, steps them in
  one ordered pass in `LateUpdate`, and detects teleports so a character moving
  a long way in one frame does not launch its chains.
- Single / Multiple mode at the top of the component, so a tail is set up by
  dragging one bone while a skirt gets the full list.
- `FluffyBones.DetectChains`, plus a **Detect chains** button in the inspector:
  finds tails, skirts, hair and capes by bone name and adds a chain for each.
- Profile slot with **New** and **Duplicate**, and the profile's settings drawn
  inline in the component so chains are tuned without leaving the character.
- `FluffyChain`: chains are collected from the bone hierarchy and simulated with
  Verlet integration and a rigid-length constraint, so bones lag behind the
  animated pose and swing back to it.
- `FluffyProfile`: stiffness with a falloff curve along the chain, drag and
  gravity. Assigned per character, overridable per chain.
- Scene view gizmos for the chains, both in play mode and while editing.

### Changed

- Minimum supported version raised to Unity 6.
- `FluffyChain` is no longer a `MonoBehaviour`. Chains are entries in a list on
  the `FluffyBones` component instead of a component per bone.
- Namespace is `Fluffy`, not `FluffyBones`. The component had to be named
  `FluffyBones` for the inspector to read "Fluffy Bones", and a type cannot
  share its name with the namespace holding it without breaking fully qualified
  references for consumers.

## [0.0.1] - 2026-08-15

### Added

- Initial package scaffolding: assembly definitions, empty runtime and editor
  stubs, test assemblies, documentation and samples folders.

[Unreleased]: https://github.com/uayten/fluffy-bones/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/uayten/fluffy-bones/releases/tag/v0.0.1
