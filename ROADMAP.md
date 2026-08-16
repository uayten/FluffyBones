# Fluffy Bones — what is built and what is missing

A working list, ordered so each step stands on the one before it. Not a wish
list: everything here is either a gap someone will hit or a thing that already
bit us once.

Status of the package itself lives in `Packages/com.uayten.fluffybones/README.md`,
and what changed lives in its `CHANGELOG.md`. This file is about what to do next.

---

## Where it stands

**Working.** Chains collected from the bone hierarchy and solved with Verlet on a
fixed step, drawn between two solved steps so an uneven frame rate never reaches
the solver, and a roll along each bone solved beside the swing so a tail unwinds
after the body that turned it. Authored default pose, shared pose assets, per-bone
angle limits on all three axes with
scene gizmos, a switch per bone for the ones the animation should keep, sphere
capsule box and plane colliders that can be turned on the bone they ride and can
ride a chain as readily as the body, a radius per chain and a tab of their own on
the character, profiles with
return strength, falloff, damping and gravity, chain detection by bone name, a
bone picker that only offers the character's own bones, and a frame-by-frame
debugger that writes CSV.

**Stubbed.** Nothing.

**Stored but not enforced.** Nothing. X Twist was the last of it.

**Missing entirely.** A user manual. Everything under "Before selling".

---

## 1. Tests — done, with two corners left

Sixty three of them: fifty five in `Tests/Editor`, eight in `Tests/Runtime`. Every
one builds its own rig in code and destroys it afterwards, so nothing depends on
a scene or an asset and nothing writes to bones that belong to one. The runtime
assembly carries no editor API at all, since it builds for every platform — which
keeps those tests on the same public surface a customer has.

All seven cases from the original list are covered:

1. **Energy does not grow on uneven frames** — `FluffyChainSolverTests`.
2. **The same seconds at different rates leave the chain in the same place** —
   `FluffyChainSolverTests` at the solver, `FluffyBodyPlayModeTests` through the
   accumulator.
3. **Limits hold** — `FluffyChainLimitsTests`, global and per-bone.
4. **`ClampRange`** — `FluffyChainLimitsTests`.
5. **A chain from the inspector is not born frozen** — `FluffyChainInspectorTests`.
6. **The dummy bone** — `FluffyDummyBoneTests`; a chain of one bone lives in
   `FluffyChainBuildTests`.
7. **`CaptureState` agrees with the solver** — `FluffyChainTraceTests`.

Writing them found one defect and fixed it: gravity was scaled by the step
squared while the pull to the pose was scaled by the step, so the simulation rate
decided how far a chain hung — 1.91 degrees off the pose at 30 steps a second,
0.95 at 60, 0.48 at 120. Both are accelerations now, and 60 still means what it
meant.

Two corners of the original list were not written the way it described, and the
difference is worth knowing:

- The uneven-frame test uses a synthetic schedule, a short step and a long one
  alternating, rather than replaying frame times recorded from a real run. The
  invariant is covered; a replay of `FluffyDebug` timings would cover it against
  this machine's own stutter, which is where it was found.
- The rate test compares 30 against 120 and 60 against 120, not the 20 and 240
  ends of the slider, and compares the angle off the pose rather than where the
  tip ended up.

Written since, beyond the seven: the pose drawn between two steps lands on the
step at the end of it and on the path between them in the middle
(`FluffyChainInterpolationTests`); the CSV is read back and every row checked
against its own header, in numbers an invariant parser can take
(`FluffyDebuggerTraceTests`); the bone slot refuses a bone from another character
(`FluffyBoneFieldTests`); and a falloff curve with no keys leaves the spring
alone rather than zeroing it (`FluffyProfileTests`).

Collision arrived with its own nine, in `FluffyColliderTests` and one in
`FluffyBodyPlayModeTests`, and gained four more when shapes learned to turn and to
ride a chain. The pose seeding that keeps a skirt from adopting a rest pose by
accident has three in `FluffyPoseSeedingTests`, and the switch that leaves a bone
to its animation has five in `FluffyChainBoneSwitchTests`. Twist has five of its
own in `FluffyChainTwistTests`: that a bone is left behind by a rig rolling under
it, that the roll travels down the chain rather than arriving everywhere at once,
that it comes home once the rig stops, that the range holds, and that a chain
which is only swinging never rolls itself.

What is left untested is what has nothing worth testing yet: most of the
inspector is IMGUI. One case is known and not written — a
collider moving faster than the chain can be pushed by it, which needs a shape
swept between two steps rather than sampled at one, and there is nothing to test
until that exists.

## 2. Collision — done, with two corners left

Sphere, capsule, box and plane, put on the bones they belong to and travelling
with the animation — which is what lifts a skirt when the leg lifts. The plane
has no size: everything on the wrong side of it is brought to the surface, so one
on the spine is the whole of "the hair never falls forward". The solver pushes a
tip out along the shortest way and puts it back on the sphere of its own bone's
length; each chain carries a radius, since a strand is a rope rather than a line.
Shapes are found at `Rebuild`, so one added at runtime needs another. When a
shape and an angle limit disagree the limit wins, and the clamp after the push is
what makes the bone slide along its own boundary rather than stop where the shape
left it.

A shape is turned as well as placed, since a bone points wherever the rig aimed it
and a thigh capsule that has to lean with the muscle cannot be aimed by choosing
between three axes.

Shapes come in two kinds, and the only difference is what they are parented to.
One sits on the body and blocks everything: a thigh, the chest, a plane on the
spine. The other rides a chain the plugin is moving, so a cape has a body the
skirt cannot walk through — it blocks every chain but the one carrying it, which
the character works out once at `Rebuild` rather than the solver asking every
step. Without that exception a bone would push a shape it is carrying, which
pushes the bone, and the strand shakes itself apart in a few frames.

Chains are solved in list order, so a shape riding chain two is where the last
step left it when chain one is solved against it. A step of lag between two
chains of the same character is not worth a second pass over both.

The character's inspector has a Collision tab listing every shape it will be
solved against, in those two groups, since the shapes live on bones and nobody
goes looking for a component on a thigh.

No rigidbodies and no physics scene, which was the point.

What is left:

- **An inside-out mode**, for a chain that has to stay within a volume rather
  than outside one. The `TODO` is on `FluffyCollider`.
- **Overlapping shapes.** One pass, in the order the character collected them, so
  two that overlap can hand a tip back and forth and it settles on whichever is
  last. Iterating costs every chain a second pass over every shape to pay for a
  case a sensibly built rig does not have — worth revisiting the day a rig has
  one.

## 3. X Twist — done

A roll angle and its speed per joint, driven by the parent's roll, damped like the
swing and clamped to the Twist range, which is what this section asked for.

The shape of it: the solver still turns a bone by the shortest arc, which carries
no roll, so the roll is solved beside it in one dimension. A bone keeps the roll
it had in the world while its rest frame turns underneath it; what it was already
doing carries on, damping takes its share each step, and the same spring that
holds the swing to its pose pulls the roll back to the rig. The step enters
squared, for the same reason it does in the swing.

A bone's rest frame is its parent's rotation, and the parent has already lagged,
so each bone is handed the share of the roll the one above it passed on. That is
what makes a tail unwind along its length rather than in one piece. It falls out
of where the roll is measured rather than out of anything the code says, which is
why there is a test pinning it.

No torque and no gravity. A bone's roll under gravity depends on where its mass
sits, which a chain of transforms does not know and no rig is going to be asked
for. `FluffyLimits.TwistEnforced` is still the one switch that turns the whole
thing off — the simulation, the field and the circle in the scene.

What is left: the trace writes the twist angle but has no `AtTwistLimit` beside
the two swing flags, so a roll pinned against its range does not show in the CSV
the way a pinned swing does.

## 4. A user manual

The README is doing two jobs: it explains the plugin to whoever is using it and
tracks what is unfinished. A buyer should never read the second half.

- `Documentation~/` holds the manual: getting started, one page per feature,
  a troubleshooting page, and a page on tuning that says what each profile value
  does to the look.
- The README keeps the summary, the installation, and the honest limitations.
- `package.json` gains `documentationUrl`.

Write the troubleshooting page from this project's own history: chains that do
not move, chains that follow rigidly, a strand that floats because its rest pose
was captured while it was somewhere else.

## 5. Before selling

- `Samples/` becomes `Samples~` with a `samples` entry in `package.json`. It is
  visible now on purpose, because Unity does not import a folder with a tilde and
  the scene could not be edited otherwise.
- `package.json`: `documentationUrl`, `unityRelease`, a real version.
- Decide whether `FluffyDebugger` ships in `Runtime`. It writes files and is
  meant for development; either it goes behind a define or it is documented as a
  tool the customer may delete.
- A demo scene with an actual character, separate from the playground, which has
  no models on purpose.

## 6. Smaller things worth doing

- **Finish splitting `FluffyBonesEditor.cs`.** Still over a thousand lines with
  the collision tab moved out of it. That one showed the seam works: one file per
  tab, as partial classes. Pose and Limits are the next two and the biggest.
- **A chain with no start bone** should say so in the inspector rather than
  drawing an empty section.
- **Undo.** The inspector writes through `SerializedObject` in most places, which
  is undoable, but the buttons that write to the object directly should all be
  recorded, and some are not.
- **Profile presets.** Cape, skirt, tail, hair, antenna — a menu of starting
  points is worth more to a buyer than any single slider.
