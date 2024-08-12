using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.MIDI
{
    public enum MetaEventType
    {
        SequenceNumber = 0,
        Text,
        Copyright,
        SequenceName,
        InstrumentName,
        Lyric,
        Marker,
        CuePoint,
        ChannelPrefix = 0x20,
        EndOfTrack = 0x2F,
        Tempo = 0x51,
        SMPTE = 0x54,
        TimeSignature = 0x58,
        KeySignature,
        Unknown = 0xFF
    }

    public struct MetaEvent
    {
        public MetaEventType type;
        public double delta;
        public byte[] data;
    }

    public enum MIDIEventType
    {
        NoteOff = 0x8,
        NoteOn,
        KeyPressure,
        ControlChange,
        ProgramChange,
        ChannelPressure,
        Pitchheel
    }

    public struct MIDIEvent
    {
        public MIDIEventType type;
        public double delta;
        public byte channel;
        public byte[] data;
    }

    public struct TempoEvent
    {
        public double delta;
        public double value;
    }

    // Notes
    public struct Note
    {
        public byte channel;
        public byte key;
        public byte velocity;
        public ulong tick;
        public ulong tickEnd;
    }

    public class TrackEventParser : IDisposable
    {
        BufferedReader reader;
        private byte prevCmd;
        private bool analysisMode;

        public ulong m_EventCount;
        public ulong m_NoteCount;
        public ulong m_TickLength;

        public List<MetaEvent> m_MetaEvents = new List<MetaEvent>();
        public List<MIDIEvent> m_MIDIEvents = new List<MIDIEvent>();
        public List<TempoEvent> m_TempoEvents = new List<TempoEvent>();

        // Notes that have no ends (yet)
        struct UnendedNote
        {
            public int id;
            public byte vel;
        }

        FastList<UnendedNote>[] UnendedNotes = null;
        int currNoteIndex = 0;

        // The actual notes
        public Note[] connectedNotes;

        public TrackEventParser(BufferedReader reader, bool analysisMode = false)
        {
            this.reader = reader;
            this.analysisMode = analysisMode;
        }

        uint ReadVLQ()
        {
            uint result = 0;

            while (true)
            {
                byte b = reader.ReadByte();
                result = (result << 7) | (uint)(b & 0x7F);
                if ((b & 0x80) == 0) break;
            }

            return result;
        }

        public bool ParseEvents()
        {
            uint delta = 0;
            bool trackEnded = false;

            while (!trackEnded)
            {
                delta = ReadVLQ();
                m_TickLength += delta;

                byte cmd = reader.ReadFast();
                if (cmd < 0x80)
                {
                    reader.Pushback = cmd;
                    cmd = prevCmd;
                }
                prevCmd = cmd;
                byte cm = (byte)(cmd & 0xF0);
                byte chan = (byte)(cmd & 0x0F);
                switch (cm)
                {
                    // Note off event
                    case 0x80:
                        {
                            byte note = reader.ReadByte();
                            byte vel = reader.ReadFast();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent
                                {
                                    type = MIDIEventType.NoteOff,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[2] { note, vel }
                                });

                                var un = UnendedNotes[note * 16 + chan];
                                if (!un.ZeroLen)
                                {
                                    un.Pop();
                                }
                            }

                            m_EventCount++;

                            break;
                        }
                    // Note on event
                    case 0x90:
                        {
                            byte note = reader.ReadByte();
                            byte vel = reader.ReadFast();

                            MIDIEventType type = MIDIEventType.NoteOn;

                            if (vel == 0)
                            {
                                type = MIDIEventType.NoteOff;
                                m_NoteCount--;
                            }

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent
                                {
                                    type = type,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[2] { note, vel }
                                });

                                if (vel != 0)
                                    UnendedNotes[note * 16 + chan].Add(new UnendedNote() { vel = vel });
                                else
                                {
                                    var un = UnendedNotes[note * 16 + chan];
                                    if (!un.ZeroLen)
                                    {
                                        un.Pop();
                                    }
                                }
                            }

                            m_EventCount++;
                            m_NoteCount++;

                            break;
                        }
                    // Key Pressure
                    case 0xA0:
                        {
                            byte note = reader.ReadByte();
                            byte pressure = reader.ReadByte();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent
                                {
                                    type = MIDIEventType.KeyPressure,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[2] { note, pressure }
                                });
                            }

                            m_EventCount++;
                            break;
                        }
                    // CC Event
                    case 0xB0:
                        {
                            byte controller = reader.ReadByte();
                            byte value = reader.ReadByte();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent()
                                {
                                    type = MIDIEventType.ControlChange,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[2] { controller, value }
                                });
                            }

                            m_EventCount++;
                            break;
                        }
                    // Program change
                    case 0xC0:
                        {
                            byte program = reader.ReadByte();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent()
                                {
                                    type = MIDIEventType.ProgramChange,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[1] { program }
                                });
                            }

                            m_EventCount++;
                            break;
                        }
                    // Channel pressure
                    case 0xD0:
                        {
                            byte pressure = reader.ReadByte();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent()
                                {
                                    type = MIDIEventType.ChannelPressure,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[1] { pressure }
                                });
                            }

                            m_EventCount++;
                            break;
                        }
                    // Pitch wheel
                    case 0xE0:
                        {
                            byte v1 = reader.ReadByte();
                            byte v2 = reader.ReadByte();

                            if (!analysisMode)
                            {
                                m_MIDIEvents.Add(new MIDIEvent()
                                {
                                    type = MIDIEventType.Pitchheel,
                                    delta = delta,
                                    channel = chan,
                                    data = new byte[2] { v1, v2 }
                                });
                            }

                            m_EventCount++;
                            break;
                        }
                    default:
                        {
                            switch (cmd)
                            {
                                case 0xF0:
                                    {
                                        while (reader.ReadByte() != 0xF7) { }
                                        break;
                                    }
                                case 0xF2:
                                    {
                                        reader.Skip(2);
                                        break;
                                    }
                                case 0xF3:
                                    {
                                        reader.Skip(1);
                                        break;
                                    }
                                // Meta events
                                case 0xFF:
                                    {
                                        byte c = reader.ReadByte();
                                        uint length = ReadVLQ();

                                        byte[] data = new byte[length];
                                        for (int i = 0; i < length; i++)
                                        {
                                            data[i] = reader.ReadByte();
                                        }

                                        MetaEventType type = MetaEventType.Unknown;

                                        switch (c)
                                        {
                                            // Text events
                                            case 0x01:
                                                {
                                                    type = MetaEventType.Text;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Copyright event
                                            case 0x02:
                                                {
                                                    type = MetaEventType.Copyright;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Sequence name
                                            case 0x03:
                                                {
                                                    type = MetaEventType.SequenceName;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Instrument name
                                            case 0x04:
                                                {
                                                    type = MetaEventType.InstrumentName;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Lyric
                                            case 0x05:
                                                {
                                                    type = MetaEventType.Lyric;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Marker
                                            case 0x06:
                                                {
                                                    type = MetaEventType.Marker;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Cue point
                                            case 0x07:
                                                {
                                                    type = MetaEventType.CuePoint;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // End of track
                                            case 0x2F:
                                                {
                                                    type = MetaEventType.EndOfTrack;
                                                    trackEnded = true;
                                                    break;
                                                }
                                            // Tempo event
                                            case 0x51:
                                                {
                                                    if (length != 3) return false;
                                                    type = MetaEventType.Tempo;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Time signature
                                            case 0x58:
                                                {
                                                    if (length != 4) return false;
                                                    type = MetaEventType.TimeSignature;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            // Key signature
                                            case 0x59:
                                                {
                                                    if (length != 2) return false;
                                                    type = MetaEventType.KeySignature;
                                                    m_EventCount++;
                                                    break;
                                                }
                                            default:
                                                {
                                                    break;
                                                }
                                        }

                                        if (!analysisMode)
                                        {
                                            if (type != MetaEventType.Unknown && type != MetaEventType.Tempo)
                                            {
                                                m_MetaEvents.Add(new MetaEvent()
                                                {
                                                    type = type,
                                                    delta = delta,
                                                    data = data
                                                });
                                            }

                                            if (type == MetaEventType.Tempo)
                                            {
                                                // convert from microseconds to tempo
                                                double encodedTempo = (data[0] << 16) | (data[1] << 8) | (data[2]);
                                                double decodedTempo = 60000000 / encodedTempo;

                                                m_TempoEvents.Add(new TempoEvent()
                                                {
                                                    delta = delta,
                                                    value = decodedTempo
                                                });
                                            }
                                        }

                                        break;
                                    }
                            }
                            break;
                        }
                }
            }
            UnendedNotes = null;

            return true;
        }

        public void PrepareNoteConnection()
        {
            reader.Reset();
            connectedNotes = new Note[m_NoteCount];
            m_TickLength = 0;
        }

        public void ConnectNotes()
        {
            UnendedNotes = new FastList<UnendedNote>[256 * 16];
            for (int i = 0; i < UnendedNotes.Length; i++)
            {
                UnendedNotes[i] = new FastList<UnendedNote>();
            }
            try
            {
                double delta = 0;
                bool trackEnded = false;
                while (!trackEnded)
                {
                    delta = ReadVLQ();
                    m_TickLength += (ulong)delta;

                    byte cmd = reader.ReadFast();
                    if (cmd < 0x80)
                    {
                        reader.Pushback = cmd;
                        cmd = prevCmd;
                    }
                    prevCmd = cmd;
                    byte cm = (byte)(cmd & 0xF0);
                    byte chan = (byte)(cmd & 0x0F);

                    switch (cm)
                    {
                        // Note off event
                        case 0x80:
                            {
                                byte note = reader.ReadByte();
                                byte vel = reader.ReadFast();
                                var un = UnendedNotes[note * 16 + chan];
                                if (!un.ZeroLen)
                                {
                                    var n = un.Pop();
                                    if (n.id != -1)
                                    {
                                        connectedNotes[n.id].tickEnd = m_TickLength;
                                    }
                                }
                                break;
                            }
                        case 0x90:
                            {
                                byte note = reader.ReadByte();
                                byte vel = reader.ReadFast();
                                if (vel == 0)
                                {
                                    var un = UnendedNotes[note * 16 + chan];
                                    if (!un.ZeroLen)
                                    {
                                        var n = un.Pop();
                                        if (n.id != -1)
                                        {
                                            connectedNotes[n.id].tickEnd = m_TickLength;
                                        }
                                    }
                                }
                                else
                                {
                                    // note threshold soon
                                    // if (vel >= noteThresh)
                                    // {
                                    UnendedNotes[note * 16 + chan].Add(new UnendedNote()
                                    {
                                        id = currNoteIndex,
                                        vel = vel
                                    });
                                    connectedNotes[currNoteIndex++] = new Note()
                                    {
                                        tick = m_TickLength,
                                        channel = chan
                                    };
                                    // ]
                                }
                                break;
                            }
                        // Key Pressure
                        case 0xA0:
                            {
                                byte note = reader.ReadByte();
                                byte pressure = reader.ReadByte();
                                break;
                            }
                        // CC Event
                        case 0xB0:
                            {
                                byte controller = reader.ReadByte();
                                byte value = reader.ReadByte();
                                break;
                            }
                        // Program change
                        case 0xC0:
                            {
                                byte program = reader.ReadByte();
                                break;
                            }
                        // Channel pressure
                        case 0xD0:
                            {
                                byte pressure = reader.ReadByte();
                                break;
                            }
                        // Pitch wheel
                        case 0xE0:
                            {
                                byte v1 = reader.ReadByte();
                                byte v2 = reader.ReadByte();
                                break;
                            }
                        default:
                            {
                                switch (cmd)
                                {
                                    case 0xF0:
                                        {
                                            while (reader.ReadByte() != 0xF7) { }
                                            break;
                                        }
                                    case 0xF2:
                                        {
                                            reader.Skip(2);
                                            break;
                                        }
                                    case 0xF3:
                                        {
                                            reader.Skip(1);
                                            break;
                                        }
                                    // Meta events
                                    case 0xFF:
                                        {
                                            byte c = reader.ReadByte();
                                            uint length = ReadVLQ();

                                            byte[] data = new byte[length];
                                            for (int i = 0; i < length; i++)
                                            {
                                                data[i] = reader.ReadByte();
                                            }

                                            MetaEventType type = MetaEventType.Unknown;

                                            switch (c)
                                            {
                                                // Text events
                                                case 0x01:
                                                    {
                                                        break;
                                                    }
                                                // Copyright event
                                                case 0x02:
                                                    {
                                                        break;
                                                    }
                                                // Sequence name
                                                case 0x03:
                                                    {
                                                        break;
                                                    }
                                                // Instrument name
                                                case 0x04:
                                                    {
                                                        break;
                                                    }
                                                // Lyric
                                                case 0x05:
                                                    {
                                                        break;
                                                    }
                                                // Marker
                                                case 0x06:
                                                    {
                                                        break;
                                                    }
                                                // Cue point
                                                case 0x07:
                                                    {
                                                        break;
                                                    }
                                                // End of track
                                                case 0x2F:
                                                    {
                                                        trackEnded = true;
                                                        break;
                                                    }
                                                // Tempo event
                                                case 0x51:
                                                    {
                                                        break;
                                                    }
                                                // Time signature
                                                case 0x58:
                                                    {
                                                        break;
                                                    }
                                                // Key signature
                                                case 0x59:
                                                    {
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        break;
                                                    }
                                            }
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }
            }
            catch { }

            for (int note = 0; note < 256; note++)
            {
                for (int chan = 0; chan < 16; chan++)
                {
                    while (!UnendedNotes[note * 16 + chan].ZeroLen)
                    {
                        var n = UnendedNotes[note * 16 + chan].Pop();
                        if (n.id != -1) connectedNotes[n.id].tickEnd = m_TickLength;
                    }
                }
            }

            // cleanup
            UnendedNotes = null;
        }

        public void Dispose()
        {
            reader.Dispose();
            m_MetaEvents = null;
            m_MIDIEvents = null;
            m_TempoEvents = null;
        }
    }
}
