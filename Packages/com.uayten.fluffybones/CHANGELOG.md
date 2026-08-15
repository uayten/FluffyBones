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
- Bone slots open a searchable dropdown of the character's own bones instead of
  Unity's object picker, which lists every transform in the scene. Dragging a
  bone in from the Hierarchy still works, and bones from outside the character
  are refused.
- Chain entries in Multiple mode are labelled with the bone they start on rather
  than "Element 7".
- Authored default pose: the component lists every bone of a chain with its
  rotation, and editing a field turns the bone in the scene as you drag, so a
  tail modelled straight can be curled without touching the model. **Capture
  from scene** and **Apply to scene** move the pose in either direction, and an
  **Editing Chain** dropdown picks which chain the list shows.
- **Show Bones**: draws the chains' bones in the scene view as wireframe
  octahedra, so posing needs no separate bone renderer. Colour is configurable,
  and the package still depends on nothing. **Show Axes** draws each bone's local
  axes alongside them.
- **Dummy Bone** on the chain, with its own toggle and an Auto length. A game
  engine bone is a single point with no length of its own, so the end of a chain
  needs one invented for it; Auto measures it from the rig, and turning Auto off
  sets the length by hand. Turning the dummy off suits rigs that already end in a
  spare bone. It draws faded.
- `FluffyPose`, a saved pose asset. Rotations are local, so one asset fits every
  chain with the same bone count and the eight strands of a skirt are posed once.
  A pose longer than the chain hands its first entries to the bones that exist;
  the extras show greyed, with a button to drop them and save the file.
- **Angle Limits**: a per-bone cap, in degrees, on how far a bone may swing from
  its pose. 180 is free, 0 pins it. Stored in the pose asset beside the
  rotations, so it is shared and copied with them.
- **Copy this chain's setup to the others**: pushes one chain's pose asset, dummy
  bone settings and profile override onto every other chain, leaving their start
  and last bones alone.

### Changed in this release

- `FluffyProfile.Stiffness` is now `ReturnStrength`, shown as "Strength to
  Return to Default Pose", and `EvaluateStiffness` is `EvaluateReturnStrength`.
  The drag value is labelled "Damping", which is what it always was.
- `FluffyGeneric`, a profile shipped inside the package and assigned to new
  components, so a chain works before anything is configured. It is read-only
  wherever the package is installed as a package; the inspector says so and
  points at Duplicate.
- `FluffyChain`: chains are collected from the bone hierarchy and simulated with
  Verlet integration and a rigid-length constraint, so bones lag behind the
  animated pose and swing back to it.
- `FluffyProfile`: stiffness with a falloff curve along the chain, drag and
  gravity. Assigned per character, overridable per chain.
- Scene view gizmos for the chains, both in play mode and while editing.

### Changed

- Minimum supported version raised to Unity 6.
- A chain is bounded by **Start Bone** and **Last Bone** instead of a root bone
  plus a tip length. Last Bone is optional and, when set, the bone below it in
  the rig is left alone but still aims the final simulated bone; a chain that
  ends at a real leaf extends by one bone length so its end still swings.
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
