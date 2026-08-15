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

1. Add **Fluffy Body** to the character root. One component per character — not
   one per bone.
2. Press **Detect chains**. It scans the skeleton for bones whose names contain
   `tail`, `skirt`, `hair`, `cape` and friends, and adds a chain for each one.
   Bones that branch off a chain already found are skipped, so `tail_02` does
   not start a second chain. Anything it misses, drag the root bone into the
   list by hand.
3. Press play and move the character. The bones lag behind and swing back.
4. To tune it, create a profile — **Assets → Create → Fluffy Bones → Profile** —
   and assign it on the body. A single chain can override it with one of its own.

| Parameter | What it does |
| --- | --- |
| Stiffness | How hard the chain returns to the animated pose. 0 leaves it limp. |
| Stiffness Falloff | Scales stiffness from root (0) to tip (1). Lower at the tip whips more. |
| Drag | Motion bled off each frame. 0 swings forever, 1 kills it instantly. |
| Gravity | Constant world acceleration. A light droop reads better than -9.81. |
| Tip Length | Virtual bone past the last real one, so the tip swings too. |
| Teleport Threshold | Root movement in one frame that snaps the chain back to rest. |

## Contents

| Type | Role |
| --- | --- |
| `FluffyBody` | The component. Goes on the character, owns its chains and steps them. **Working.** |
| `FluffyChain` | One bone chain, held in a list on the body. Not a component. **Working.** |
| `FluffyProfile` | ScriptableObject with the tuning values, shareable between chains. **Working.** |
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
