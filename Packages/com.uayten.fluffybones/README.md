# Fluffy Bones — Cute Bone Physics

Secondary motion for bone chains in Unity. Point it at a tail, a skirt, a cape,
a chain or a lock of hair and it follows the character with springy, tunable
physics — no rigidbodies, no joints, no physics scene setup.

> **Status: early WIP.** The chain solver, the angle limits and the per-character
> update pass work. Collision is not implemented yet, and neither is the twist
> half of the limits — see [Known limitations](#known-limitations).

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
| Strength to Return to Default Pose | How hard the chain pulls back to its pose. 0 leaves it limp. |
| Strength Falloff Along Chain | Scales that strength from start (0) to end (1). Lower at the end whips more. |
| Damping | Motion bled off each frame. 0 swings forever, 1 kills it instantly. This is what stops wobble. |
| Gravity | Constant world acceleration. A light droop reads better than -9.81. |
| Teleport Threshold | Character movement in one frame that snaps the chains back to rest. |

The two spring values are easy to mix up. **Strength** decides *where* the chain
wants to be; **damping** decides *how fast it stops moving*. Wobble that will not
settle is a damping problem — raising the strength makes it worse, because a
stronger spring oscillates faster.

## Default pose

The chains spring back to a pose, and by default that pose is the one the model
was imported with. When the model does not have the pose you want — a tail
modelled straight that should curl at the end — you do not have to go back to
Blender:

The Default Pose section lists every bone of the chain with its rotation. Type in
those fields and the bone turns in the scene as you drag — that is the fastest
way to dial in a curve. Or rotate the bones in the scene with the normal tools
and press **Capture from scene** to read them back in. **Apply to scene** pushes
the stored pose back onto the bones after play mode or an animation has moved
them.

In Multiple mode an **Editing Chain** dropdown picks which chain the list is
showing.

Posing eight skirt strands one at a time would be miserable, so there are two
ways out. Assign a **Pose Asset** and the chain reads from that file — rotations
are local, so giving all eight strands the same asset poses them once. **Copy
this chain's setup to the others** pushes the current chain's pose asset, dummy
bone settings and profile override onto every other chain in one press; start
and last bones are left alone, since those belong to the strand.

A pose does not have to match the chain's length. Its entries are handed to the
bones in order, so a pose written for ten bones drives the first three of a
three-bone tail and the rest are ignored. Those extra entries show greyed in the
list, with a button to drop them and write the file back out.

With no pose asset the rotations live on the component, so the model file is
untouched and two characters sharing one mesh can rest differently.

**Show Bones** draws the chain's bones in the scene view as wireframe octahedra,
the shape a skeleton is normally drawn with. It is there so posing does not need
a separate bone renderer component — Fluffy Bones has no dependency on Animation
Rigging or anything else. **Show Axes** adds each bone's local axes, X red, Y
green, Z blue.

## Angle limits

Under **Angle Limits**, a bone may turn only so far from its pose, set as a
minimum and a maximum on each of its own axes, in degrees. -180 to 180 leaves an
axis free, which is the default and draws nothing.

- **Y Swing** and **Z Swing** open the cone the bone moves inside.
- **X Twist** is the roll along the bone, drawn as a circle. Saved and drawn, but
  not yet enforced — see [Known limitations](#known-limitations).

The angles are measured from the pose, so 0 is where the bone rests: the minimum
cannot go above it and the maximum cannot go below it. A range that shut the pose
out would ask for a bone held somewhere the spring pulls it straight out of.

Drag sideways on **Min** or **Max** to scrub the value, the same as the rotation
fields above. A drag stops when it reaches the pose.

Each axis keeps its colour throughout — Y green, Z blue, X red — in the field
labels and in the shapes drawn in the scene, so the arc you are looking at names
the field you need to edit.

Limits are set once under **Global** and every bone follows them. A bone that
needs to differ gets **Override** ticked and its own three axes; **Reset to
global** puts it back. That is the usual shape of a chain: the whole cape moves
alike except the two bones at the shoulders.

Separate minimums and maximums are what a cape needs. Resting against the back,
it should billow out when the character runs and barely move when they back up —
so the axis that carries that motion gets a large limit one way and a small one
the other. A single symmetric angle cannot express it: raise it and the cape
swings wildly both ways, lower it and it never billows at all.

For a skirt, tight limits on the upper bones are what keep a strand from folding
through a leg, and there the two sides are usually equal.

Tick **Show Limits** to see them. Everything starts at the bone's head: a green
arc for the Y range, a blue arc for the Z range, the rim they make together in
the limit colour, and a red circle around the bone for the twist. All of it is
lopsided whenever a minimum and maximum differ, so a glance tells you which way a
bone is free to go.

The limits are read in the bone's own axes, which assumes the bone runs along its
local X — the usual result of an export — so if an arc looks turned the wrong
way, switch on **Show Axes** and check.

The limits live in the same asset as the rotations, so they are shared and copied
along with them. They are per bone rather than per profile because a limit
describes the rig — where the leg is — while a profile describes feel.

## The dummy bone

A bone in Blender runs from a head to a tail and has a length. In a game engine
it is a single point with a rotation — the length you see is only the gap to the
next bone, so the last bone of a chain has none at all, and nothing to swing
towards.

**Dummy Bone** invents one for it. **Auto** measures its length: the bone below
the chain when the rig has one, otherwise the length of the bone before it. Turn
Auto off to set **Dummy Bone Length** by hand — a longer dummy makes the end of
the chain swing wider and slower. It draws faded, so it never reads as a bone the
rig actually has.

Turn the dummy off when the rig already ends in a spare bone put there for this
purpose. The last real bone is then left to follow its parent instead of being
simulated, since it has nothing to aim at.

These sit on the chain rather than on the profile on purpose: a profile is feel
and gets shared between a tail and a skirt, while a dummy length is geometry and
belongs to one rig.

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
- **X Twist** does not restrict anything yet. The solver swings the bone towards
  its tip and never rolls it, so there is no twist to clamp; the limit is stored,
  drawn and copied like the other two, ready for when there is.
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
