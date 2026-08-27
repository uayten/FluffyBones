# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- **X Twist** is simulated, so the range that was stored all along now holds
  something back. The solver turns a bone by the shortest arc from its rest
  direction, which carries no roll by construction, so a bone whose parent rolled
  used to follow that roll rigidly on the same frame — the one motion in the chain
  with no secondary motion at all. The roll is now solved in one dimension on the
  same terms as the swing: the bone keeps the roll it had in the world while the
  rig turns underneath it, what it was already doing carries on, damping takes its
  share, and a spring of the same strength pulls it back to the rig. A bone is
  measured against its own parent rather than against the character, so the roll
  travels down a tail instead of arriving everywhere at once. No torque and no
  gravity: where a bone's mass sits is not something a chain of transforms knows.
- A switch per bone, in the Setup tab, for the bones a chain should leave to the
  animation. A chain takes everything between its two ends, which is right nearly
  always and wrong at the top — the first bone of a skirt usually belongs to the
  hip animation. Saying so used to mean moving the chain's start bone down one and
  losing that bone's pose and limits with it. Bones below a switched-off one carry
  on swinging from wherever it puts them. Stored in the pose beside the per-bone
  limits, so a shared pose asset turns the same bone off on every strand at once.
- Collision shapes take a **Rotation** on the bone they ride. A bone points
  wherever the rig aimed it, and a thigh capsule that leans with the muscle cannot
  be aimed by choosing between three axes. Everything with a direction goes through
  it — the capsule's axis, the plane's normal, the box's frame, the gizmo.
- Collision shapes can ride a chain instead of the body, which is how a cape gets a
  body of its own that the skirt cannot walk through. Such a shape pushes every
  chain except the one carrying it: a bone pushed out of a shape it carries pushes
  the shape back, and the strand shakes itself apart within a few frames. The
  character pairs each chain with the shapes it faces once at `Rebuild`. Chains are
  solved in list order, so a shape riding one is where the last step left it when
  the next is solved against it.
- A bone carrying a shape is as thick as that shape when it is pushed out of the
  blockers, and the chain's own thickness is the fallback for the bones that carry
  none. One number for a whole chain cannot say that a cape is wide at the
  shoulders and narrow at the hem, and a shape on a bone already says how wide the
  chain is there — it is what the other chains bump into. The same capsule now does
  both jobs, and the width is authored by dragging it in the scene rather than
  typed. A sphere and a capsule are as thick as their radius; a box as thick as its
  narrowest half, since a cape panel is a flat box and the flat direction is the
  answer; a plane has no thickness and falls back to the number.
- **Show Thickness**, in the Collision tab: draws each chain as a tube of the width
  it is actually solved at. That width is added to every shape the chain meets, so
  a shape is drawn at its own size while pushing from further out — set by
  accident, it reads as a collider reaching across the room to shove a chain
  nowhere near it, with nothing to see. The chain thickness field also says its
  unit now, and warns when it is out of scale with the chain's own bones: 1 against
  bones a quarter of a unit long is four bones of skin on every shape.
- The Collision tab keeps the two kinds of shape apart, each with its own Add row
  whose bone picker offers only the bones that make a shape of that kind. They are
  the same component and differ only in what they are parented to, so nothing else
  would stop a blocker being put on a cape bone by mistake.
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
- **Angle Limits**: a minimum and a maximum per bone on each of its own axes. Y
  and Z open the cone the bone swings inside, X is the twist along it.
  Separate minimums and maximums make the cone lopsided, which is what lets a
  cape billow far off the back and barely move the other way. Stored in the
  rotation asset beside the pose, so both are shared and copied together.
- Limits are set globally per chain and every bone follows them, until a bone
  ticks **Override** and carries its own. **Reset to global** puts it back.
- The limit fields scrub by dragging, like the rotation fields. One label sits
  over two numbers there, so the axis letter cannot be the handle the way it is
  for a rotation: **Min** and **Max** are the handles, tinted the axis colour.
- `FluffyLimits.ClampRange` keeps the pose inside every range — the minimum at or
  below 0, the maximum at or above it. A range that shut the pose out asked for a
  bone held where the spring pulls it straight out of, and drew a rim turned
  inside out. Applied in the inspector and again on the way to the solver, since
  the fields are public and a pose asset can be edited from elsewhere.
- **Show Limits**: draws them from each bone's head, each axis in its own colour
  — a green arc for Y, a blue one for Z, and a red circle for the twist. The
  field labels carry the same colours. One flat arc per axis rather than the rim
  of the cone the two make together: the rim is the truthful shape, but a chain
  of them reads as a knot of ellipses, and an arc is what a number can be read
  off. The **Limit Colour** setting went with the rim, which was the only thing
  it painted.
- **Copy this chain's setup to the others**: pushes one chain's pose asset, dummy
  bone settings and profile override onto every other chain, leaving their start
  and last bones alone.
- **Limit Size**, beside Show Limits: scales the drawn limit shapes against the
  bone's length, for when neighbouring bones' shapes run into each other.
- A trace opens with the settings it was recorded under — profile numbers,
  teleport distance, every recorded bone with its limits, frame rate cap, time
  scale — as comment lines. Fixed for the whole recording, so written once rather
  than repeated on every row, and read off the objects so the file cannot
  disagree with the run that made it.
- `ownTurnDeg` beside `turnDeg`: how far a bone turned against its own rest frame
  rather than in the world. A bone deep in a chain inherits most of its world
  motion from its parents, so comparing world turn between bones says more about
  the parents than about the bone.
- **Fluffy Debugger** writes only the groups of columns asked for — Timing,
  Motion, Angles, Bounds, Positions — with frame, chain and bone always there.
  Twenty-nine columns hide the three that answer a question, and Bounds in
  particular repeated the same limits on every row. The default is Timing, Motion
  and Angles, twelve columns. Header and row are built from the same checks in
  the same order, since a header that disagrees with its rows is wrong quietly.
- **Fluffy Debugger** takes a **Start Bone** and an **End Bone** instead of a
  chain index and a bone, both from the character's own dropdown. An empty End
  Bone records the start bone alone, which is the opposite of what the same field
  means on Fluffy Bones and is what a recording usually wants; the inspector
  spells out which of the two you are getting. Its Start and Stop buttons are
  drawn greyed outside play mode rather than replaced by a note, because a button
  that is not there reads as a feature that is not there.
- **Fluffy Debugger** records a frame range on its own, **From Frame** to **To
  Frame**, counted from the start of play. The opening frames are never the
  interesting ones, and choosing the window beforehand beats trying to catch the
  moment with a button. Starting and stopping by hand are buttons rather than a
  tick box, and the bone to record is picked from the character's own bones the
  way Start Bone is, instead of typed as a name to match.
- **Fluffy Debugger**, a component that records what the chains did frame by
  frame to a CSV: how far each bone turned, how long the frame it turned in was,
  where it sits in the frame the limits are measured in, and whether it is pinned
  against one of them. Something that goes wrong for three frames cannot be caught
  by eye, and a still cannot tell a pop from motion that is merely fast — the
  frame length beside the turn can. `FluffyChain.CaptureState` reads the same
  decomposition the solver clamps in, and `FluffyBones` now reports the last
  frame's length, step count and whether the chains were carried.

### Changed in this release

- A character may carry more than one `FluffyBones`. Chains that behave nothing
  alike — a heavy tail and the light stripes down a trouser leg — can have a
  component each, which reads better in the inspector than one list whose entries
  disagree; a single component in Multiple mode with a per-chain profile override
  still does the same job. Components sharing a GameObject each see the whole
  character's shapes, so their chains are still pushed out of each other. The
  inspector warns when two of them drive the same bone, which is the one
  arrangement that does not work: both write to it every frame and the winner is
  whichever Unity runs last.
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

- The component's inspector is a row of tabs — Setup, Pose, Limits, Collision,
  Behaviour, Advanced — instead of one column of foldouts. Reaching the angle
  limits used to mean scrolling past a rotation field for every bone in the chain.
  The tab you were last in is remembered. A dot marked the tabs holding something
  other than their default for a while; it landed on nearly every tab of a
  character that was set up at all, and a mark on everything marks nothing.
- Every chain is given a pose as soon as the inspector opens, not only the one in
  the Editing Chain dropdown. A chain without one adopts whatever its bones happen
  to be when it is built, so anything that moves those transforms first silently
  becomes the pose it springs back to.

### Fixed

- The chains are solved on a fixed step and drawn between two of them, so an
  uneven frame rate no longer reaches the solver at all. Every correction before
  this one — splitting long frames, scaling damping by time, scaling the stored
  motion by the ratio between steps — was chasing the same cause one symptom at a
  time, and none of them could finish the job while the step itself still varied.
  Measured on a skirt against a recording's own frame times: 16 direction
  reversals a second and a worst drawn step of 16.5 degrees became 1.9 and 2.4,
  against 0.7 and 1.0 for a perfectly even frame rate. **Simulation Rate** sets
  the step, 60 a second by default.

  A plugin for secondary motion cannot ask for a locked frame rate, which is what
  the honest version of the earlier advice amounted to.
- The gizmo and trace paths walk the bone hierarchy into one list they keep
  rather than building a new one per shape. With bones, axes and limits all
  drawn, an eight-chain skirt was building two dozen of them a repaint.
- Damping was applied once per step, so what it meant depended on how many steps a
  second there happened to be. A drag of 0.15 leaves 38% of a bone's speed after
  100 ms at 60 fps, 72% at 20, and 0.9% at 292 — which a small scene in the editor
  reaches easily. At that rate a chain arrives with no inertia left to carry it, so
  it stops travelling through its range and starts being placed wherever the spring
  and the head put it, against one limit or the other, which reads as a bone
  jumping rather than swinging. It is raised to the length of the step now, so it
  is the same at any frame rate and unchanged at 60, where every existing profile
  was tuned.
- A dropped frame threw the chains. The solver stepped once per rendered frame
  with whatever time had passed, and every term is proportional to it: at Unity's
  own limit for a stalled frame, a third of a second, the spring alone moved a tip
  further than its bone is long in a single step, so the chain was flung and spent
  the following frames coming back. Measured on the playground tail, a bone turned
  at 9600 degrees a second around a stall against 660 in normal motion. A frame is
  now split into steps of about a sixtieth of a second, up to sixteen of them, and
  the same stall peaks at 500 — below normal motion. Damping is applied per step,
  so this settles how the chain behaves at different frame rates too: two seconds
  of the same movement at 20 fps and at 60 fps used to leave the tip 0.55 units
  apart and now leave it 0.07.
- A character that jumps further than the chains can swing through is carried
  along rigidly instead of being snapped back to its rest pose. The snap threw
  away the pose and the motion, and that discard was itself a visible pop; the
  chains now arrive with the character keeping both. `Teleport Threshold` became
  **Teleport Distance** to say what it measures — a distance between two frames,
  not a speed, because what the solver cannot swing through is how far the bones'
  heads moved between two of its samples.
- Limits edited while the chain was running never reached the solver. They were
  copied into the joints when the chain was built, so the fields and the drawn
  shape showed the new numbers while the bones went on obeying the old ones —
  an axis locked to 0 to 0 mid-play still swung 62 degrees. The solver reads them
  as they are now, per bone per step; the joints no longer carry a copy.
- The limit shapes turned with the bone they belong to, so there was no telling
  how far through its range it had travelled — the shape moved exactly as much as
  the bone did. They are anchored to the bone's rest frame now, the same one the
  solver clamps in, so the shape stays where the limit is and the bone travels
  inside it. It still follows the parent, because the limit does.
- The swing arcs were drawn in the wrong plane on any bone with roll. The frame
  was built from the bone's world direction, so "towards Y" came out of world up,
  while the solver builds it from the bone's own axis — 35 degrees apart on a
  bone with 55 degrees of roll, and only agreeing at all on a rig with none.
- A free axis drew nothing, so ticking **Show Limits** on a bone at -180 to 180
  showed an empty scene and left you unable to tell "free" from "not drawn yet".
  Every shape now spans its own range throughout: nothing at 0 to 0, a full
  circle at -180 to 180. Segment counts follow the sweep, so a narrow arc is not
  drawn with as many lines as a whole turn and a whole turn is not a polygon.
- The dummy bone did not turn with the bone it belongs to. Its tip was invented
  by carrying straight on from the bone before it, which matches the bone's own
  rotation only while the chain is straight — the moment the last bone turned,
  posed or simulated, the dummy stayed pointing the way the chain used to go. It
  now comes off the bone's rotation, the axis read from the previous bone rather
  than assumed to be X. Identical at rest, so no rig moves and the solver is
  unaffected; the limit gizmos on the last bone follow it too now.
- A chain of one bone threw. Dragging a leaf bone into **Start Bone** left the
  invented tip reaching for `bones[-1]`. It measures from the bone's parent in
  the rig now, and from nothing but its own forward if it has none.
- Picking **Multiple** sprang straight back to Single. The Default Pose section
  refreshed the component's own `SerializedObject` halfway through drawing the
  inspector, which threw away everything edited above it that frame — the mode,
  the chain list, the dummy bone toggles. Only the asset's is refreshed now, the
  way the matching apply at the end of that method always did.
- The twist ring said the opposite of what it meant: a full circle at 0 to 0,
  where the bone may not roll at all, and nothing at -180 to 180, where it may
  roll the whole way round. It now spans the range and no more.
- A chain created by the inspector was born frozen. Unity builds a new list entry
  by zeroing it rather than by running the class's field initialisers, and every
  zero in a `FluffyChain` means the opposite of its default: no dummy bone, no
  automatic length, and limits of 0 to 0 on all three axes, which the solver
  reads as a bone that may not leave its pose. It hit the + button in Multiple
  mode and the first chain of a brand new component alike — the bones simply
  followed the animation and nothing swung. New entries are now filled in with
  the defaults; entries Unity filled by copying the one before them are already
  authored and are left alone.

## [0.0.1] - 2026-08-15

### Added

- Initial package scaffolding: assembly definitions, empty runtime and editor
  stubs, test assemblies, documentation and samples folders.

[Unreleased]: https://github.com/uayten/fluffy-bones/compare/v0.0.1...HEAD
[0.0.1]: https://github.com/uayten/fluffy-bones/releases/tag/v0.0.1
