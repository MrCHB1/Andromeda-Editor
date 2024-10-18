using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.MIDI
{
    public enum ParseStatus
    {
        ParseSuccess,
        ParseError
    }

    public delegate void ParseProgress();

    public class MIDIParser : IDisposable
    {
        // MIDI Information
        public ushort PPQ;
        public ushort TrackCount;
        public uint TrueTrackCount;
        public ushort Format;
        public ulong NoteCount;
        public ulong EventCount;
        public ulong LengthTicks;

        private bool analysisMode;

        // Magic Texts?
        private uint MThd = 0x4D546864;
        private uint MTrk = 0x4D54726B;

        private Stream fileStream;

        // Tracks to point to for parallelization purposes
        public struct TrackPointer
        {
            public long Start;
            public uint Length;
        }

        public List<TrackPointer> TrackPointers = new List<TrackPointer>();

        // Stuff related to MIDI parsing progress
        public event ParseProgress ParseStarted;
        public event ParseProgress ParseError;
        public event ParseProgress ParseUpdate;
        public event ParseProgress ParseFinished;

        long tracksParsed = 0;
        public double nTracksCompletion
        {
            get => tracksParsed / (double)tracksParsed * 100.0;
        }

        /// <summary>
        /// Parses a MIDI file.
        /// </summary>
        /// <param name="path">The file path to the MIDI file.</param>
        /// <param name="analysisMode">Analysis mode does not save events to a list. Used for Analyzing a MIDI file.</param>
        public MIDIParser(string path, bool analysisMode = false)
        {
            fileStream = File.Open(path, FileMode.Open);
            this.analysisMode = analysisMode;
            LengthTicks = 0;
        }

        // Parses the MIDI step by step
        public ParseStatus ParseMIDI()
        {
            ParseStarted?.Invoke();

            ParseStatus status;

            // Parse header first
            status = ParseHeader();
            if (status == ParseStatus.ParseError)
            {
                ParseError?.Invoke();
                return status;
            }

            // Populate the track pointers before parallel parsing
            status = PopulateTrackPointers();
            if (status == ParseStatus.ParseError)
            {
                ParseError?.Invoke();
                return status;
            }

            BufferedReader[] readers = new BufferedReader[TrueTrackCount];
            TrackEventParser[] parsers = new TrackEventParser[TrueTrackCount];

            // Parallel parsing across all tracks
            Parallel.For(0, TrueTrackCount, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (track) =>
            {
                long trackPos = TrackPointers[(int)track].Start;
                long trackLen = TrackPointers[(int)track].Length;
                readers[track] = new BufferedReader(fileStream, 100000, trackPos, trackLen);
                parsers[track] = new TrackEventParser(readers[track], analysisMode);

                bool success = parsers[track].ParseEvents();
                if (!success)
                {
                    status = ParseStatus.ParseError;
                    return;
                }

                // update midi info after parsing all
                NoteCount += parsers[track].m_NoteCount;
                EventCount += parsers[track].m_EventCount;
                if (parsers[track].m_TickLength > LengthTicks) LengthTicks = parsers[track].m_TickLength;

                tracksParsed++;
            });

            ParseFinished?.Invoke();
            return status;
        }

        /// <summary>
        /// Parses the first 14 bytes of the MIDI file.
        /// </summary>
        /// <returns>Returns <c>ParseStatus.ParseSuccess</c> if the MIDI Header is correct.</returns>
        ParseStatus ParseHeader()
        {
            // check if first four bytes is MThd
            if (ReadUInt() != MThd) return ParseStatus.ParseError;

            // check if the header length is 6
            if (ReadUInt() != 6) return ParseStatus.ParseError;

            Format = ReadUShort();
            TrackCount = ReadUShort(); // Track Count is assumed.
            PPQ = ReadUShort();

            return ParseStatus.ParseSuccess;
        }

        ParseStatus PopulateTrackPointers()
        {
            while (fileStream.Position < fileStream.Length)
            {
                if (ReadUInt() != MTrk) return ParseStatus.ParseError;
                uint trackLength = ReadUInt();
                long trackStart = fileStream.Position;

                TrackPointers.Add(new TrackPointer { Start = trackStart, Length = trackLength });
                fileStream.Seek((long)trackLength, SeekOrigin.Current);
            }

            TrueTrackCount = (uint)TrackPointers.Count;
            return ParseStatus.ParseSuccess;
        }

        uint ReadUInt()
        {
            uint read = 0;
            for (int i = 0; i != 4; i++)
            {
                read = ((read << 8) | (uint)fileStream.ReadByte());
            }
            return read;
        }

        ushort ReadUShort()
        {
            ushort read = 0;
            for (int i = 0; i != 2; i++)
            {
                read = (ushort)((read << 8) | fileStream.ReadByte());
            }
            return read;
        }

        public void Dispose()
        {
            TrackPointers.Clear();
            fileStream.Close();
            fileStream.Dispose();
        }
    }
}
