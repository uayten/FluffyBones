using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fluffy
{
    /// <summary>
    /// Groups of columns a recording can carry.
    /// </summary>
    /// <remarks>
    /// Every column costs a reader's attention, and most questions need a handful. The
    /// frame, the chain and the bone are always written, since a row without them says
    /// nothing about what it describes.
    /// </remarks>
    [System.Flags]
    public enum FluffyDebugColumns
    {
        /// <summary>Nothing but the frame and which bone it is.</summary>
        None = 0,

        /// <summary>How long the frame was: time, deltaTime, steps, carried.</summary>
        Timing = 1 << 0,

        /// <summary>How far the bone turned: turnDeg, turnDegPerSec, offRestDeg.</summary>
        Motion = 1 << 1,

        /// <summary>Where it sits: swingY, swingZ, twist, and whether it is pinned.</summary>
        Angles = 1 << 2,

        /// <summary>The limits themselves, which repeat unchanged on every row.</summary>
        Bounds = 1 << 3,

        /// <summary>Head, direction and character position, in world space.</summary>
        Positions = 1 << 4,

        /// <summary>What answers most questions without burying them.</summary>
        Default = Timing | Motion | Angles
    }

    /// <summary>
    /// Records what the chains did, frame by frame, into a file that can be read away
    /// from the running game.
    /// </summary>
    /// <remarks>
    /// A chain that looks wrong for three frames cannot be caught by eye, and a
    /// screenshot cannot tell a pop from motion that is simply fast — the two look the
    /// same in a still. This writes the numbers instead: how far each bone turned, how
    /// long the frame it turned in was, and how far through its limits it had travelled.
    /// A bone that turned 20 degrees in a frame that lasted a third of a second was
    /// moving slower than usual; the same 20 degrees in a sixtieth is a pop.
    ///
    /// Runs after the solver, which the execution order below guarantees, so every row
    /// is the state the frame ended in rather than the one it started from.
    /// </remarks>
    [DefaultExecutionOrder(1000)]
    [AddComponentMenu("Fluffy Bones/Fluffy Debugger")]
    public class FluffyDebugger : MonoBehaviour
    {
        [Tooltip("The character to record. Empty uses the Fluffy Bones on this object.")]
        [SerializeField] private FluffyBones _body;

        [Tooltip("Where to start recording. Empty records every bone of every chain, " +
                 "which for a skirt is hundreds of rows a second.")]
        [SerializeField] private Transform _startBone;

        [Tooltip("Where to stop, included. Empty records the start bone ALONE — not the " +
                 "rest of the chain, which is what the same field means on Fluffy Bones. " +
                 "One bone is what you usually want to read.")]
        [SerializeField] private Transform _endBone;

        [Tooltip("Start and stop by frame number instead of by hand. The first frames of " +
                 "play are never the interesting ones — the chains are still settling and " +
                 "the editor is still warming up.")]
        [SerializeField] private bool _automatic;

        [Tooltip("Frame to start at, counting from the moment play began.")]
        [Min(0)]
        [SerializeField] private int _fromFrame = 120;

        [Tooltip("Frame to stop and write out at.")]
        [Min(1)]
        [SerializeField] private int _toFrame = 420;

        [Tooltip("A recording started by hand writes itself out after this many frames, " +
                 "so one left running does not eat memory.")]
        [Min(1)]
        [SerializeField] private int _maxFrames = 3600;

        [Tooltip("Which groups of columns to write. Frame, chain and bone are always " +
                 "there; everything else costs a reader's attention, and most questions " +
                 "need a handful of columns.")]
        [SerializeField] private FluffyDebugColumns _columns = FluffyDebugColumns.Default;

        [Tooltip("Folder for the files, beside the project in the editor and in the " +
                 "persistent data path in a build.")]
        [SerializeField] private string _folder = "FluffyDebug";

        private readonly List<FluffyBoneState> _states = new List<FluffyBoneState>();
        private readonly List<Transform> _recorded = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> _previousDirections = new Dictionary<Transform, Vector3>();
        private readonly Dictionary<Transform, Vector3> _previousLocalDirections = new Dictionary<Transform, Vector3>();
        private StringBuilder _rows;
        private int _fieldsWritten;
        private int _firstFrame;
        private int _frames;
        private bool _recording;
        private bool _automaticDone;

        /// <summary>The character being recorded.</summary>
        public FluffyBones Body => _body;

        /// <summary>Whether a recording is running.</summary>
        public bool IsRecording => _recording;

        /// <summary>How many frames the running recording has captured.</summary>
        public int RecordedFrames => _frames;

        /// <summary>
        /// Frames since play began, which is what the frame range and the trace's own
        /// frame column count in.
        /// </summary>
        public int Frame => Time.frameCount - _firstFrame;

        /// <summary>Whether the automatic range has already been through.</summary>
        public bool AutomaticDone => _automaticDone;

        /// <summary>Starts a recording, discarding anything held from a previous one.</summary>
        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            _previousDirections.Clear();
            _previousLocalDirections.Clear();

            // Before the preamble, not after: the preamble lists the bones it is about to
            // record, and asking that question before the answer exists listed every bone
            // in the chain while the rows held one.
            ResolveBones();

            _rows = new StringBuilder(1 << 16);
            AppendPreamble();
            AppendHeader();

            _frames = 0;
            _recording = true;
        }

        /// <summary>
        /// Which bones the recording covers, worked out once when it starts.
        /// </summary>
        /// <remarks>
        /// An empty end bone means the start bone alone, which is the opposite of what
        /// the same pair means on <see cref="FluffyBones"/>, where it runs to the end of
        /// the hierarchy. Deliberate: a chain is set up once and wants its whole length,
        /// while a recording is read by eye afterwards and one bone is usually the point.
        /// </remarks>
        private void ResolveBones()
        {
            _recorded.Clear();

            if (_startBone == null)
            {
                return;
            }

            if (_endBone == null)
            {
                _recorded.Add(_startBone);
                return;
            }

            _recorded.AddRange(FluffyChain.CollectChain(_startBone, _endBone));
        }

        /// <summary>Stops the recording and writes it out.</summary>
        /// <returns>The file written, or null when there was nothing to write.</returns>
        [ContextMenu("Stop and Save")]
        public string StopRecording()
        {
            _recording = false;

            if (_rows == null || _frames == 0)
            {
                Debug.LogWarning("[Fluffy Bones] Nothing was recorded.", this);
                return null;
            }

            string directory = ResolveFolder();
            Directory.CreateDirectory(directory);

            string file = Path.Combine(
                directory,
                $"FluffyDebug_{name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");

            File.WriteAllText(file, _rows.ToString());
            _rows = null;

            Debug.Log($"[Fluffy Bones] Wrote {_frames} frames to {file}", this);
            return file;
        }

        /// <summary>Where the files go.</summary>
        public string ResolveFolder()
        {
            if (Path.IsPathRooted(_folder))
            {
                return _folder;
            }

            // Beside the project in the editor, where it can be found and handed over;
            // a build has no project folder to sit next to.
            string root = Application.isEditor
                ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                : Application.persistentDataPath;

            return Path.Combine(root, _folder);
        }

        private void Reset()
        {
            _body = GetComponent<FluffyBones>();
        }

        private void OnEnable()
        {
            if (_body == null)
            {
                _body = GetComponent<FluffyBones>();
            }

            _firstFrame = Time.frameCount;
            _automaticDone = false;
        }

        private void OnValidate()
        {
            _toFrame = Mathf.Max(_toFrame, _fromFrame + 1);
        }

        private void OnDisable()
        {
            if (_recording)
            {
                StopRecording();
            }
        }

        private void LateUpdate()
        {
            // The range is read every frame rather than armed once, so moving it while
            // play is running takes effect on the next pass through.
            if (_automatic && !_automaticDone)
            {
                int frame = Frame;

                if (!_recording && frame >= _fromFrame && frame < _toFrame)
                {
                    StartRecording();
                }
                else if (_recording && frame >= _toFrame)
                {
                    StopRecording();
                    _automaticDone = true;
                    return;
                }
            }

            if (!_recording || _body == null)
            {
                return;
            }

            IReadOnlyList<FluffyChain> chains = _body.Chains;
            Vector3 root = _body.transform.position;
            float deltaTime = _body.LastDeltaTime;

            for (int c = 0; c < chains.Count; c++)
            {
                _states.Clear();
                chains[c].CaptureState(_states);

                for (int i = 0; i < _states.Count; i++)
                {
                    FluffyBoneState state = _states[i];

                    if (!Matches(state.Bone))
                    {
                        continue;
                    }

                    // Two turns, because they answer different questions: how far the bone
                    // swung in the world, which a deep bone inherits most of from its
                    // parents, and how far it turned against its own rest frame, which is
                    // only its own.
                    float turn = 0f;
                    float ownTurn = 0f;

                    if (_previousDirections.TryGetValue(state.Bone, out Vector3 previous))
                    {
                        turn = Vector3.Angle(previous, state.Direction);
                    }

                    if (_previousLocalDirections.TryGetValue(state.Bone, out Vector3 previousLocal))
                    {
                        ownTurn = Vector3.Angle(previousLocal, state.LocalDirection);
                    }

                    _previousDirections[state.Bone] = state.Direction;
                    _previousLocalDirections[state.Bone] = state.LocalDirection;
                    AppendRow(c, state, turn, ownTurn, deltaTime, root);
                }
            }

            _frames++;

            // The automatic range has its own end; this is the guard for one started by
            // hand and forgotten about.
            if (!_automatic && _frames >= _maxFrames)
            {
                Debug.Log($"[Fluffy Bones] Recording hit its {_maxFrames} frame limit.", this);
                StopRecording();
            }
        }

        private bool Matches(Transform bone)
        {
            return _recorded.Count == 0 || _recorded.Contains(bone);
        }

        /// <summary>Whether a group of columns is switched on.</summary>
        private bool Writes(FluffyDebugColumns group)
        {
            return (_columns & group) != 0;
        }

        /// <summary>
        /// The settings the recording was made under, written once at the top as comment
        /// lines.
        /// </summary>
        /// <remarks>
        /// Everything here is fixed for the whole recording, so a column would repeat it
        /// on every row — which is what the limit bounds used to do. Read from the objects
        /// rather than typed, so a file can never disagree with the run that produced it.
        /// A leading # is what spreadsheets and parsers alike skip over.
        /// </remarks>
        private void AppendPreamble()
        {
            var inv = CultureInfo.InvariantCulture;

            Comment($"Fluffy Bones trace  {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Comment($"unity            {Application.unityVersion}"
                    + $"   targetFrameRate {Application.targetFrameRate}"
                    + $"   vSync {QualitySettings.vSyncCount}"
                    + $"   timeScale {Time.timeScale.ToString("0.##", inv)}");

            if (_body == null)
            {
                Comment("character        none");
                return;
            }

            Comment($"character        {_body.name}   mode {_body.Mode}   chains {_body.Chains.Count}"
                    + $"   teleportDistance {_body.TeleportDistance.ToString("0.###", inv)}");

            AppendProfileComment("profile", _body.Profile, inv);

            for (int c = 0; c < _body.Chains.Count; c++)
            {
                FluffyChain chain = _body.Chains[c];

                if (chain.ProfileOverride != null)
                {
                    AppendProfileComment($"chain {c} profile", chain.ProfileOverride, inv);
                }

                _states.Clear();
                chain.CaptureState(_states);

                for (int i = 0; i < _states.Count; i++)
                {
                    FluffyBoneState state = _states[i];

                    if (!Matches(state.Bone))
                    {
                        continue;
                    }

                    FluffyLimits limits = state.Limits;
                    Comment($"bone             {state.Bone.name}"
                            + $"   length {state.Length.ToString("0.###", inv)}"
                            + $"   swingY {Range(limits.SwingY, inv)}"
                            + $"   swingZ {Range(limits.SwingZ, inv)}"
                            + $"   twist {Range(limits.Twist, inv)}");
                }
            }

            Comment($"columns          {_columns}");
        }

        private void AppendProfileComment(string label, FluffyProfile profile, CultureInfo inv)
        {
            if (profile == null)
            {
                Comment($"{label,-16} none, so the solver's own defaults are in use");
                return;
            }

            Comment($"{label,-16} {profile.name}"
                    + $"   returnStrength {profile.ReturnStrength.ToString("0.###", inv)}"
                    + $"   falloff {profile.EvaluateReturnStrength(0f).ToString("0.##", inv)}"
                    + $"..{profile.EvaluateReturnStrength(1f).ToString("0.##", inv)}"
                    + $"   damping {profile.Drag.ToString("0.###", inv)}"
                    + $"   gravity {profile.Gravity.ToString("0.##")}");
        }

        private static string Range(Vector2 range, CultureInfo inv)
        {
            return $"{range.x.ToString("0.##", inv)}..{range.y.ToString("0.##", inv)}";
        }

        private void Comment(string line)
        {
            _rows.Append("# ").AppendLine(line);
        }

        /// <summary>
        /// The column names, in the order the rows are written.
        /// </summary>
        /// <remarks>
        /// Header and row are built from the same checks in the same order on purpose. A
        /// file whose header does not match its rows is worse than one with columns nobody
        /// wanted, because it is wrong quietly.
        /// </remarks>
        private void AppendHeader()
        {
            _fieldsWritten = 0;

            Field("frame");
            Field("chain");
            Field("bone");
            Field("boneName");

            if (Writes(FluffyDebugColumns.Timing))
            {
                Field("time");
                Field("deltaTime");
                Field("steps");
                Field("carried");
            }

            if (Writes(FluffyDebugColumns.Motion))
            {
                Field("turnDeg");
                Field("ownTurnDeg");
                Field("turnDegPerSec");
                Field("offRestDeg");
            }

            if (Writes(FluffyDebugColumns.Angles))
            {
                Field("swingY");
                Field("swingZ");
                Field("twist");
                Field("atYLimit");
                Field("atZLimit");
            }

            if (Writes(FluffyDebugColumns.Bounds))
            {
                Field("swingYMin");
                Field("swingYMax");
                Field("swingZMin");
                Field("swingZMax");
            }

            if (Writes(FluffyDebugColumns.Positions))
            {
                Field("headX"); Field("headY"); Field("headZ");
                Field("dirX"); Field("dirY"); Field("dirZ");
                Field("rootX"); Field("rootY"); Field("rootZ");
            }

            _rows.AppendLine();
        }

        private void AppendRow(
            int chain, FluffyBoneState state, float turn, float ownTurn, float deltaTime, Vector3 root)
        {
            var invariant = CultureInfo.InvariantCulture;
            _fieldsWritten = 0;

            // Counted from the start of play, not from the start of the recording, so a
            // row lines up with the frame range that asked for it.
            Field(Frame.ToString(invariant));
            Field(chain.ToString(invariant));
            Field(state.Index.ToString(invariant));
            Field(state.Bone.name);

            if (Writes(FluffyDebugColumns.Timing))
            {
                Field(Time.timeAsDouble.ToString("F4", invariant));
                Field(deltaTime.ToString("F5", invariant));
                Field(_body.LastStepCount.ToString(invariant));
                Field(_body.CarriedLastFrame ? "1" : "0");
            }

            if (Writes(FluffyDebugColumns.Motion))
            {
                Field(turn.ToString("F3", invariant));
                Field(ownTurn.ToString("F3", invariant));
                Field((deltaTime > 0f ? turn / deltaTime : 0f).ToString("F1", invariant));
                Field(state.OffRest.ToString("F3", invariant));
            }

            if (Writes(FluffyDebugColumns.Angles))
            {
                Field(state.SwingY.ToString("F3", invariant));
                Field(state.SwingZ.ToString("F3", invariant));
                Field(state.Twist.ToString("F3", invariant));
                Field(state.AtSwingYLimit ? "1" : "0");
                Field(state.AtSwingZLimit ? "1" : "0");
            }

            if (Writes(FluffyDebugColumns.Bounds))
            {
                Field(state.Limits.SwingY.x.ToString("F2", invariant));
                Field(state.Limits.SwingY.y.ToString("F2", invariant));
                Field(state.Limits.SwingZ.x.ToString("F2", invariant));
                Field(state.Limits.SwingZ.y.ToString("F2", invariant));
            }

            if (Writes(FluffyDebugColumns.Positions))
            {
                AppendVector(state.Head, invariant);
                AppendVector(state.Direction, invariant);
                AppendVector(root, invariant);
            }

            _rows.AppendLine();
        }

        /// <summary>
        /// One field, with the comma before it when it is not the first of its row.
        /// </summary>
        /// <remarks>
        /// Invariant formatting is the caller's job throughout: a comma for a decimal
        /// point turns a CSV into nonsense, and this machine's locale uses one.
        /// </remarks>
        private void Field(string value)
        {
            if (_fieldsWritten > 0)
            {
                _rows.Append(',');
            }

            _rows.Append(value);
            _fieldsWritten++;
        }

        private void AppendVector(Vector3 value, CultureInfo invariant)
        {
            Field(value.x.ToString("F4", invariant));
            Field(value.y.ToString("F4", invariant));
            Field(value.z.ToString("F4", invariant));
        }
    }
}
