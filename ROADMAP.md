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
the solver. Authored default pose, shared pose assets, per-bone angle limits with
scene gizmos, sphere capsule box and plane colliders with a radius per chain and
a tab of their own on the character, profiles with
return strength, falloff, damping and gravity, chain detection by bone name, a
bone picker that only offers the character's own bones, and a frame-by-frame
debugger that writes CSV.

**Stubbed.** Nothing.

**Stored but not enforced.** X Twist. The solver produces pure swing; the field
is saved, drawn and copied like the other two and restricts nothing.

**Missing entirely.** A user manual. Everything under "Before selling".

---

## 1. Tests — done, with two corners left

Forty nine of them: forty one in `Tests/Editor`, eight in `Tests/Runtime`. Every
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
`FluffyBodyPlayModeTests`. The pose seeding that keeps a skirt from adopting a
rest pose by accident has three in `FluffyPoseSeedingTests`.

What is left untested is what has nothing worth testing yet: most of the
inspector is IMGUI, and twist has no code. One case is known and not written — a
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

The character's inspector has a Collision tab listing every shape it will be
solved against, since the shapes live on bones and nobody goes looking for a
component on a thigh.

No rigidbodies and no physics scene, which was the point.

What is left:

- **An inside-out mode**, for a chain that has to stay within a volume rather
  than outside one. The `TODO` is on `FluffyCollider`.
- **Overlapping shapes.** One pass, in the order the character collected them, so
  two that overlap can hand a tip back and forth and it settles on whichever is
  last. Iterating costs every chain a second pass over every shape to pay for a
  case a sensibly built rig does not have — worth revisiting the day a rig has
  one.

## 3. X Twist, for real

The solver rotates a bone by the shortest arc from its rest direction to its new
one, which by construction carries no roll. Giving the twist meaning needs a roll
angle and an angular speed per joint, driven by the parent's roll, damped like
the swing, and clamped to the Twist range.

Until it exists the field is hidden, so nothing in the inspector promises what
the solver does not do. Unhiding is one flag.

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
