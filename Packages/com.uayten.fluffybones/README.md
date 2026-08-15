# Fluffy Bones — Cute Bone Physics

Secondary motion for bone chains in Unity. Point it at a tail, a skirt, a cape,
a chain or a lock of hair and it follows the character with springy, tunable
physics — no rigidbodies, no joints, no physics scene setup.

> **Status: early WIP.** The chain solver works. Collision, angle limits and the
> per-character update pass are not implemented yet.

## Requirements

- Unity 6 (6000.0) or newer

## Installation

The package is currently developed as an embedded package. In the shipping
version it will be installed through the Package Manager.

To use it from another project in the meantime, add a local reference to the
project's `Packages/manifest.json`:

```json
"com.uayten.fluffybones": "file:../../FluffyBones/Packages/com.uayten.fluffybones"
```

The path is relative to the consuming project's `Packages` folder.

## Getting started

1. Add **Fluffy Bones** to the character root. One component per character — not
   one per bone.
2. Choose the mode at the top:
   - **Single** — one chain. Drag the first bone of the tail into **Start Bone**
     and you are done. **Last Bone** is optional: leave it empty and the chain
     runs to the end of the hierarchy; set it to stop earlier, which is how you
     keep the solver off a bone that is rigged for something else.
   - **Multiple** — many chains sharing one profile, which is what a skirt is.
     Press **Detect chains** to scan the skeleton for bones named `tail`,
     `skirt`, `hair`, `cape` and friends. Bones inside a chain already found are
     skipped, so `tail_02` does not start a second one. Anything it misses, drag
     the start bone into the list by hand.
3. Press play and move the character. The bones lag behind and swing back.

A **profile** is the behaviour asset. New components start on `FluffyGeneric`,
which ships with the package, so a chain behaves sensibly before you touch a
slider. That one is read-only — press **Duplicate** to get a copy you can tune,
or **New** for an empty one. Its settings are drawn right there in the
component, so you never leave the character to adjust them.

Profiles are normal assets: reuse one across chains and characters, duplicate it
for a variant, edit it and every chain using it updates at once.

| Parameter | What it does |
| --- | --- |
| Stiffness | How hard the chain returns to the animated pose. 0 leaves it limp. |
| Stiffness Falloff | Scales stiffness from start (0) to end (1). Lower at the end whips more. |
| Drag | Motion bled off each frame. 0 swings forever, 1 kills it instantly. |
| Gravity | Constant world acceleration. A light droop reads better than -9.81. |
| Teleport Threshold | Character movement in one frame that snaps the chains back to rest. |

## Contents

All types live in the `Fluffy` namespace.

| Type | Role |
| --- | --- |
| `FluffyBones` | The component. Goes on the character, owns its chains and steps them. **Working.** |
| `FluffyChain` | One bone chain, held in a list on the component. Not a component itself. **Working.** |
| `FluffyProfile` | The behaviour asset — the tuning values, shareable and duplicable. **Working.** |
| `FluffyCollider` | Collision shape the chains are pushed out of. *Stub.* |

## Known limitations

- The solver steps once per rendered frame, so behaviour changes with frame
  rate. Substepping on a fixed timestep is still to be done.
- Chains are collected by following the first child. Bones that branch, and
  chains defined by an explicit bone list, are not supported yet.

## Assemblies

| Assembly | Platforms |
| --- | --- |
| `FluffyBones.Runtime` | All |
| `FluffyBones.Editor` | Editor only |
| `FluffyBones.Tests.Runtime` | All, test-only |
| `FluffyBones.Tests.Editor` | Editor only, test-only |

## License

Proprietary — all rights reserved. See [LICENSE.md](LICENSE.md).
