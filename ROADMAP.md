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
scene gizmos, profiles with return strength, falloff, damping and gravity, chain
detection by bone name, a bone picker that only offers the character's own bones,
and a frame-by-frame debugger that writes CSV.

**Stubbed.** `FluffyCollider` — the type exists and does nothing.

**Stored but not enforced.** X Twist. The solver produces pure swing; the field
is saved, drawn and copied like the other two and restricts nothing.

**Missing entirely.** A user manual. Everything under "Before selling".

---

## 1. Tests — done, with two corners left

Twenty two of them: nineteen in `Tests/Editor`, three in `Tests/Runtime`. Every
one builds its own rig in code and destroys it afterwards, so nothing depends on
a scene or an asset and nothing writes to bones that belong to one.

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

Still untested, in rough order of what would hurt: the interpolation between two
steps is only checked end to end and nothing asserts the drawn pose sits between
them; the CSV `FluffyDebugger` writes has no test at all; the inspectors have
none either. Collision and twist have nothing to test yet.

## 2. Collision

`FluffyCollider` is a stub, and "the skirt goes through the leg" is the first
thing anyone will report. What it needs:

- Sphere and capsule colliders, assigned per character, pushing tips out along
  the shortest way.
- A radius per chain, since a skirt strand is not a line.
- Colliders found once at build rather than searched per frame.
- Deciding what happens when a bone is pushed somewhere its angle limit forbids:
  the limit should win, and the bone should slide along it.

Physics colliders are the obvious alternative, and the wrong one — the whole
point of the plugin is that it needs no rigidbodies and no physics scene.

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

- **Split `FluffyBonesEditor.cs`.** It is over a thousand lines. The tab work
  gives the seams: one file per tab, as partial classes.
- **Seed the pose for every chain, not just the one being edited.** A chain with
  no authored pose adopts whatever its bones happen to be at build time, so
  anything that touches those transforms first silently becomes the rest pose.
  This has already caused one wrecked scene.
- **A chain with no start bone** should say so in the inspector rather than
  drawing an empty section.
- **Undo.** The inspector writes through `SerializedObject` in most places, which
  is undoable, but the buttons that write to the object directly should all be
  recorded, and some are not.
- **Profile presets.** Cape, skirt, tail, hair, antenna — a menu of starting
  points is worth more to a buyer than any single slider.
