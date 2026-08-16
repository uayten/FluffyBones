using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fluffy.Tests
{
    /// <summary>
    /// The CSV the debugger writes, read back and checked.
    /// </summary>
    /// <remarks>
    /// A trace is written once and read hours later, usually by someone trying to work
    /// out why a chain did something. A file that is subtly wrong at that point costs far
    /// more than the recording did: the reader trusts it, and the numbers look plausible
    /// either way.
    /// </remarks>
    public class FluffyDebuggerTraceTests
    {
        private readonly FluffyRuntimeRig _rig = new FluffyRuntimeRig();
        private string _written;

        [SetUp]
        public void RememberTheClock()
        {
            _rig.RememberTheClock();
        }

        [TearDown]
        public void PutTheClockBackAndTidyUp()
        {
            if (!string.IsNullOrEmpty(_written) && File.Exists(_written))
            {
                File.Delete(_written);
            }

            _written = null;
            _rig.PutTheClockBackAndTidyUp();
        }

        /// <summary>
        /// A recording produces a file whose rows all fit its own header, in numbers a
        /// parser can read anywhere in the world.
        /// </summary>
        /// <remarks>
        /// The invariant formatting is the part worth guarding hardest, and the machine
        /// this was written on is the right one to guard it: its locale writes a decimal
        /// comma, so a single ToString that forgets to say invariant turns every row into
        /// twice as many fields and the file into nonsense. The failure is silent — a
        /// spreadsheet opens it happily and shows numbers that are off by orders of
        /// magnitude.
        ///
        /// The header and the rows are built from the same checks in the same order, and
        /// the point of counting fields is that a file whose header disagrees with its
        /// rows is worse than one with columns nobody wanted, because it is wrong quietly.
        /// </remarks>
        [UnityTest]
        public IEnumerator ATraceIsWrittenWithRowsThatFitItsOwnHeader()
        {
            FluffyBones body = _rig.BuildCharacter();
            FluffyDebugger debugger = body.gameObject.AddComponent<FluffyDebugger>();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 1);

            debugger.StartRecording();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 20);

            int recorded = debugger.RecordedFrames;
            _written = debugger.StopRecording();

            Assert.That(recorded, Is.GreaterThan(10), $"Only {recorded} frames were recorded.");
            Assert.That(_written, Is.Not.Null.And.Not.Empty, "The recording wrote no file.");
            Assert.That(File.Exists(_written), Is.True, $"No file at '{_written}'.");

            string[] lines = File.ReadAllLines(_written);
            var preamble = new List<string>();
            string header = null;
            var rows = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                if (lines[i][0] == '#')
                {
                    Assert.That(header, Is.Null, "A comment line turned up after the header.");
                    preamble.Add(lines[i]);
                }
                else if (header == null)
                {
                    header = lines[i];
                }
                else
                {
                    rows.Add(lines[i]);
                }
            }

            Assert.That(preamble, Is.Not.Empty, "The file carries no preamble, so it does not say what it is.");
            Assert.That(header, Is.Not.Null, "The file has no header row.");

            string[] columns = header.Split(',');

            Assert.That(columns[0], Is.EqualTo("frame"));
            Assert.That(rows, Is.Not.Empty, "The file has a header and nothing under it.");

            int previousFrame = int.MinValue;

            for (int i = 0; i < rows.Count; i++)
            {
                string[] fields = rows[i].Split(',');

                Assert.That(fields, Has.Length.EqualTo(columns.Length),
                    $"Row {i} has {fields.Length} fields against a header of {columns.Length}. "
                    + "A decimal comma written by a locale-aware ToString does exactly this.");

                Assert.That(int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int frame), Is.True, $"Row {i} has an unreadable frame number: '{fields[0]}'.");

                Assert.That(frame, Is.GreaterThanOrEqualTo(previousFrame),
                    $"Row {i} goes back in time, from frame {previousFrame} to {frame}.");

                previousFrame = frame;

                for (int f = 0; f < fields.Length; f++)
                {
                    // The bone's name is the one field that is not a number.
                    if (columns[f] == "boneName")
                    {
                        continue;
                    }

                    Assert.That(double.TryParse(fields[f], NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        Is.True,
                        $"Row {i}, column '{columns[f]}' reads '{fields[f]}', which no invariant parser "
                        + "can take.");
                }
            }
        }

        /// <summary>
        /// A bone whose name carries a comma does not push every column after it along.
        /// </summary>
        /// <remarks>
        /// Bone names come from whoever rigged the character, and a comma in one is not
        /// exotic. Unquoted, it adds a field to that bone's rows and to no others: the
        /// header still lines up for every other bone in the file, so it reads as correct
        /// and is not. The failure lands on whoever opens the trace weeks later.
        /// </remarks>
        [UnityTest]
        public IEnumerator ABoneNameWithACommaDoesNotShiftTheColumns()
        {
            const string awkward = "tail_02, the long one";

            FluffyBones body = _rig.BuildCharacter();
            FluffyDebugger debugger = body.gameObject.AddComponent<FluffyDebugger>();

            foreach (Transform bone in body.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "tail_02")
                {
                    bone.name = awkward;
                }
            }

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 1);

            debugger.StartRecording();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 5);

            _written = debugger.StopRecording();

            string[] lines = File.ReadAllLines(_written);
            int columns = 0;
            bool sawTheName = false;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0 || lines[i][0] == '#')
                {
                    continue;
                }

                if (columns == 0)
                {
                    columns = lines[i].Split(',').Length;
                    continue;
                }

                Assert.That(FieldsOf(lines[i]), Has.Length.EqualTo(columns),
                    $"A row about a bone named '{awkward}' has {FieldsOf(lines[i]).Length} fields "
                    + $"against a header of {columns}.");

                if (lines[i].Contains(awkward))
                {
                    sawTheName = true;
                }
            }

            Assert.That(sawTheName, Is.True, "The awkward bone never turned up in the file.");
        }

        /// <summary>
        /// Splits a row the way a CSV reader would: commas separate fields unless they
        /// are inside quotes.
        /// </summary>
        private static string[] FieldsOf(string row)
        {
            var fields = new List<string>();
            var field = new System.Text.StringBuilder();
            bool quoted = false;

            for (int i = 0; i < row.Length; i++)
            {
                char c = row[i];

                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == ',' && !quoted)
                {
                    fields.Add(field.ToString());
                    field.Clear();
                }
                else
                {
                    field.Append(c);
                }
            }

            fields.Add(field.ToString());

            return fields.ToArray();
        }

        /// <summary>
        /// The preamble says what the recording was made under, read off the objects
        /// rather than typed.
        /// </summary>
        /// <remarks>
        /// Two traces are compared by hand far more often than one is read alone — the
        /// whole point of recording before and after a change — and a file that does not
        /// carry its own settings cannot be compared with anything. It also cannot
        /// disagree with the run that produced it, which is the reason the values are
        /// read rather than written down.
        /// </remarks>
        [UnityTest]
        public IEnumerator ThePreambleSaysWhatTheRecordingWasMadeUnder()
        {
            FluffyBones body = _rig.BuildCharacter();
            FluffyDebugger debugger = body.gameObject.AddComponent<FluffyDebugger>();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 1);

            debugger.StartRecording();

            yield return FluffyRuntimeRig.FramesOf(1f / 60f, 5);

            _written = debugger.StopRecording();

            string text = File.ReadAllText(_written);

            Assert.That(text, Does.Contain("unity            " + Application.unityVersion),
                "The preamble does not say which Unity wrote the file.");
            Assert.That(text, Does.Contain(body.name), "The preamble does not name the character.");
            Assert.That(text, Does.Contain("damping"), "The preamble does not carry the profile's tuning.");
            Assert.That(text, Does.Contain("columns"), "The preamble does not say which columns are in the file.");

            foreach (Transform bone in body.GetComponentsInChildren<Transform>())
            {
                if (bone.name.StartsWith("tail_"))
                {
                    Assert.That(text, Does.Contain(bone.name),
                        $"The preamble does not mention '{bone.name}', which the recording covers.");
                }
            }
        }
    }
}
