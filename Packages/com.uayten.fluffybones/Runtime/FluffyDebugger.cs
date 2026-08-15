using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Fluffy
{
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

        [Tooltip("Which chain to record, counting from the top of the list. -1 records " +
                 "every chain, which for a skirt is a lot of rows.")]
        [SerializeField] private int _chain = -1;

        [Tooltip("Record this bone alone. Empty records every bone of the chain above. " +
                 "Picking one narrows a skirt from hundreds of rows a second to four.")]
        [SerializeField] private Transform _bone;

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

        [Tooltip("Folder for the files, beside the project in the editor and in the " +
                 "persistent data path in a build.")]
        [SerializeField] private string _folder = "FluffyDebug";

        private readonly List<FluffyBoneState> _states = new List<FluffyBoneState>();
        private readonly Dictionary<Transform, Vector3> _previousDirections = new Dictionary<Transform, Vector3>();
        private StringBuilder _rows;
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
            _rows = new StringBuilder(1 << 16);
            _rows.AppendLine("frame,time,deltaTime,steps,carried,chain,bone,boneName,"
                             + "turnDeg,turnDegPerSec,offRestDeg,swingY,swingZ,twist,"
                             + "swingYMin,swingYMax,swingZMin,swingZMax,atYLimit,atZLimit,"
                             + "headX,headY,headZ,dirX,dirY,dirZ,rootX,rootY,rootZ");

            _previousDirections.Clear();
            _frames = 0;
            _recording = true;
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
                if (_chain >= 0 && c != _chain)
                {
                    continue;
                }

                _states.Clear();
                chains[c].CaptureState(_states);

                for (int i = 0; i < _states.Count; i++)
                {
                    FluffyBoneState state = _states[i];

                    if (!Matches(state.Bone))
                    {
                        continue;
                    }

                    float turn = 0f;
                    if (_previousDirections.TryGetValue(state.Bone, out Vector3 previous))
                    {
                        turn = Vector3.Angle(previous, state.Direction);
                    }

                    _previousDirections[state.Bone] = state.Direction;
                    AppendRow(c, state, turn, deltaTime, root);
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
            return _bone == null || bone == _bone;
        }

        private void AppendRow(int chain, FluffyBoneState state, float turn, float deltaTime, Vector3 root)
        {
            var invariant = CultureInfo.InvariantCulture;

            // Invariant throughout: a comma for a decimal point turns a CSV into
            // nonsense, and this machine's locale uses one.
            // Counted from the start of play, not from the start of the recording, so a
            // row lines up with the frame range that asked for it.
            _rows.Append(Frame.ToString(invariant)).Append(',')
                .Append(Time.timeAsDouble.ToString("F4", invariant)).Append(',')
                .Append(deltaTime.ToString("F5", invariant)).Append(',')
                .Append(_body.LastStepCount.ToString(invariant)).Append(',')
                .Append(_body.CarriedLastFrame ? '1' : '0').Append(',')
                .Append(chain.ToString(invariant)).Append(',')
                .Append(state.Index.ToString(invariant)).Append(',')
                .Append(state.Bone.name).Append(',')
                .Append(turn.ToString("F3", invariant)).Append(',')
                .Append((deltaTime > 0f ? turn / deltaTime : 0f).ToString("F1", invariant)).Append(',')
                .Append(state.OffRest.ToString("F3", invariant)).Append(',')
                .Append(state.SwingY.ToString("F3", invariant)).Append(',')
                .Append(state.SwingZ.ToString("F3", invariant)).Append(',')
                .Append(state.Twist.ToString("F3", invariant)).Append(',')
                .Append(state.Limits.SwingY.x.ToString("F2", invariant)).Append(',')
                .Append(state.Limits.SwingY.y.ToString("F2", invariant)).Append(',')
                .Append(state.Limits.SwingZ.x.ToString("F2", invariant)).Append(',')
                .Append(state.Limits.SwingZ.y.ToString("F2", invariant)).Append(',')
                .Append(state.AtSwingYLimit ? '1' : '0').Append(',')
                .Append(state.AtSwingZLimit ? '1' : '0').Append(',');

            AppendVector(state.Head, invariant);
            AppendVector(state.Direction, invariant);
            AppendVector(root, invariant, last: true);

            _rows.AppendLine();
        }

        private void AppendVector(Vector3 value, CultureInfo invariant, bool last = false)
        {
            _rows.Append(value.x.ToString("F4", invariant)).Append(',')
                .Append(value.y.ToString("F4", invariant)).Append(',')
                .Append(value.z.ToString("F4", invariant));

            if (!last)
            {
                _rows.Append(',');
            }
        }
    }
}
