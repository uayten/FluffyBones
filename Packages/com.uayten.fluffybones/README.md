# Fluffy Bones — Cute Bone Physics

Secondary motion for bone chains in Unity. Point it at a tail, a skirt, a cape,
a chain or a lock of hair and it follows the character with springy, tunable
physics — no rigidbodies, no joints, no physics scene setup.

> **Status: early WIP.** This version is scaffolding only. There is no solver
> yet — the classes below are empty stubs.

## Requirements

- Unity 2022.3 LTS or newer

## Installation

The package is currently developed as an embedded package. In the shipping
version it will be installed through the Package Manager.

To use it from another project in the meantime, add a local reference to the
project's `Packages/manifest.json`:

```json
"com.uayten.fluffybones": "file:../../FluffyBones/Packages/com.uayten.fluffybones"
```

The path is relative to the consuming project's `Packages` folder.

## Contents

| Type | Role |
| --- | --- |
| `FluffyBody` | Per-character root that owns and updates the chains. |
| `FluffyChain` | One bone chain — the component you put on a tail or a skirt strand. |
| `FluffyCollider` | Collision shape the chains are pushed out of. |
| `FluffyProfile` | ScriptableObject with the tuning values, shareable between chains. |

## Assemblies

| Assembly | Platforms |
| --- | --- |
| `FluffyBones.Runtime` | All |
| `FluffyBones.Editor` | Editor only |
| `FluffyBones.Tests.Runtime` | All, test-only |
| `FluffyBones.Tests.Editor` | Editor only, test-only |

## License

Proprietary — all rights reserved. See [LICENSE.md](LICENSE.md).
