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
| Damping | Motion bled off every sixtieth of a second. 0 swings forever, 1 kills it instantly. This is what stops wobble. Counted in time, not in frames, so a chain settles the same at 30 fps and at 300. |
| Gravity | Constant world acceleration. A light droop reads better than -9.81. |
| Simulation Rate | How many times a second the chains are solved. Steps are this long whatever the frame rate, and what is drawn is worked out between the last two — so a chain behaves the same on every machine and does not care that frames arrive unevenly. Higher is stiffer and costs more; 60 suits most characters. |
| Teleport Distance | How far the character may move between two frames before the chains are carried along rigidly instead of swinging, in world units. Past this there is no sensible swing to compute, so they travel with the character keeping their shape. Around a bone's length suits most rigs. |

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
axis free, which is the default.

- **Y Swing** and **Z Swing** open the cone the bone moves inside.
- **X Twist** is the roll along the bone. It is hidden for now: the solver turns a
  bone by the shortest arc from where it rests to where it points, and a shortest
  arc carries no roll, so there is no twist to hold back. The value is still
  stored, shared and copied, and the field comes back the day the solver rolls.

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

> **Keep a global limit symmetric.** Each bone's Y and Z are its own, and a chain
> that curls turns them as it goes: on a hanging tail, the first bone's Y points
> one way in the world and the last bone's Y points the opposite way. A range of
> -25 to 0 therefore means "may lag backwards" on one bone and "may lag forwards"
> on another, so moving the character one way frees half the chain and welds the
> other half to it. Symmetric ranges do not care which way the frame is turned.
> An asymmetric range belongs on one bone at a time, through **Override**, where
> you can see which way that bone's axes point.

Separate minimums and maximums are what a cape needs. Resting against the back,
it should billow out when the character runs and barely move when they back up —
so the axis that carries that motion gets a large limit one way and a small one
the other. A single symmetric angle cannot express it: raise it and the cape
swings wildly both ways, lower it and it never billows at all.

For a skirt, tight limits on the upper bones are what keep a strand from folding
through a leg, and there the two sides are usually equal.

Tick **Show Limits** to see them. Both start at the bone's head: a green arc for
the Y range and a blue arc for the Z range. The twist ring is hidden with the
twist field itself.

Every shape spans its own range and no more. It is lopsided whenever a minimum
and maximum differ, so a glance tells you which way a bone is free to go; it
disappears at 0 to 0, where the bone may not move on that axis at all; and it
closes into a full circle at -180 to 180, where the bone is free. A default bone
therefore wears two circles — which is what "free on every axis" looks like, and
is the honest picture. Untick Show Limits when it is in the way, or turn
**Limit Size** down: it scales every shape against the bone's length, which is
what to reach for when the shapes of neighbouring bones run into each other.

The shapes hang off the bone's **rest** direction, not off where the bone is now.
That is the whole point of watching them in play mode: the shape stays where the
limit is and you see the bone travel through its range and stop against the edge.
It still follows the bone's parent, because the limit does — a skirt strand's
range swings with the hips and holds the strand inside it.

To watch it in the game view, turn on its **Gizmos** button, in the toolbar along
the top of the view. Gizmos are an editor thing: they draw in the game view while
you play in the editor, but never in a build.

One flat arc per axis, rather than the rim of the cone the two make together. The
rim is the truthful shape, but a chain of them reads as a knot of ellipses, and an
arc is the shape you can read a number off — it lies in the plane its axis swings
in and names the field that sets it.

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

## Tracing what a bone did

Something that goes wrong for three frames cannot be caught by eye, and a
screenshot cannot tell a pop from motion that is simply fast — in a still they
look the same. **Fluffy Debugger** writes the numbers instead.

Add it beside Fluffy Bones and press play. **Start Record** and **Stop Record**
do it by hand; the console prints the path of the file written. A recording
started by hand stops itself at **Max Frames** so one left running does not eat
memory.

Tick **Automatic** instead and it records **From Frame** to **To Frame**,
counting from the moment play began. That is usually what you want: the opening
frames of play are never the interesting ones, with the chains still settling and
the editor still warming up, and picking the range beforehand beats trying to
catch the moment with a button.

**Start Bone** and **End Bone** choose what goes in the file, from the same
dropdown of the character's own bones the chain's slots use. Leave both empty and
every bone of every chain is recorded, which for a skirt is hundreds of rows a
second.

> **End Bone means something different here.** On Fluffy Bones, an empty Last
> Bone runs the chain to the end of the hierarchy. On the debugger, an empty End
> Bone records the start bone **alone**. A chain is set up once and wants its
> whole length; a recording is read by eye afterwards, and one bone is usually
> the point. The inspector says which you are getting under the two fields.

The file opens with the settings it was recorded under, as comment lines starting
with `#`: the profile's numbers, the teleport distance, each recorded bone with
its limits, the frame rate cap and the time scale. They are fixed for the whole
recording, so they are written once instead of repeated on every row — which is
what the limit columns used to do — and they are read off the objects, so a file
can never disagree with the run that made it.

**Columns** decides what goes in a row, in groups. Frame, chain and bone are
always there; the rest are worth switching on for the question at hand and off
again, because a file with twenty-nine columns hides the three that answer it.
**Timing**, **Motion** and **Angles** together are the usual set and are the
default. **Bounds** repeats the same limits on every row and is only worth having
when they are being changed while it records; **Positions** is for when the
question is where a bone is rather than what it did.

Each row is one bone in one frame:

| Group | Column | What it tells you |
| --- | --- | --- |
| always | `frame` | Counted from the start of play, so it lines up with the range that asked for it. |
| Timing | `deltaTime`, `steps` | How long the frame was and how many steps it was split into. A bone that turns a long way in a long frame was moving at its usual speed; the same turn in a sixtieth of a second is a pop. |
| Timing | `carried` | Whether the character moved far enough that frame for the chains to be carried rather than swung. |
| Motion | `turnDeg`, `turnDegPerSec` | How far the bone turned in the world since the last frame. Per second is the honest one to compare. |
| Motion | `offRestDeg` | How far it is from where its pose puts it. |
| Angles | `swingY`, `swingZ`, `twist` | Where the bone sits in the frame the limits are measured in — the same numbers the fields in the inspector set. |
| Angles | `atYLimit`, `atZLimit` | Whether it is pinned against one end of its range. |
| Bounds | `swingYMin` … | The limits themselves, the same on every row. |
| Positions | `headX` … | Head, direction and character position, in world space. |

The distinction the first two columns make is the point: turning 20 degrees in a
frame that lasted a third of a second is slower than usual, while 20 degrees in a
sixtieth is something to explain.

Files land beside the project in the editor, in `FluffyDebug`, and in the
persistent data path in a build — so a trace can be asked of someone playing a
build and read back later.

## The playground scene

`Samples/Playground/FluffyPlayground.unity` is there to try things in: a six-bone
tail in Single mode and an eight-strand skirt in Multiple mode, both found by
**Detect chains** from their bone names, and a **Fluffy Movement Test** component that
walks each character from side to side so the chains have something to react to.
Press play and watch it from the scene view. Right-click Fluffy Movement Test and pick
**Teleport** to jump the character and watch Teleport Distance carry the chains
along instead of letting them be flung.

It has no models, no materials and no lights. The bones are empty transforms and
everything you see is drawn by **Show Bones**, **Show Axes** and **Show Limits**.
That is on purpose: it opens identically in Built-in, URP and HDRP, which a scene
with one material in it would not. It also means the Game view shows nothing —
this is a scene you watch in the scene view.

The bones run along their own local X, the way an exported rig does, so the angle
limit gizmos line up with the axes the inspector names.

The folder loses its tilde on purpose. `Samples~` is what the Package Manager
imports from, but Unity does not import a folder with one at all, so the scene
could not be opened or edited while the plugin is being built. It becomes
`Samples~`, with a `samples` entry in `package.json`, at publish.

## Contents

All types live in the `Fluffy` namespace.

| Type | Role |
| --- | --- |
| `FluffyBones` | The component. Goes on the character, owns its chains and steps them. **Working.** |
| `FluffyChain` | One bone chain, held in a list on the component. Not a component itself. **Working.** |
| `FluffyProfile` | The behaviour asset — the tuning values, shareable and duplicable. **Working.** |
| `FluffyDebugger` | Records what the chains did, frame by frame, to a CSV. **Working.** |
| `FluffyCollider` | Collision shape the chains are pushed out of. *Stub.* |

## Known limitations

- A frame that falls more than eight steps behind gives up the rest of the time
  it owes rather than trying to catch up, so a machine that stutters badly enough
  will see the chains fall behind for a moment instead of stalling further.
- Drawn poses are interpolated between two solved steps along a straight line, so
  a bone held hard against a limit can be drawn a fraction of a degree outside it
  — half a degree at a limit of twelve, in a chain moving as fast as it ever
  does.
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
