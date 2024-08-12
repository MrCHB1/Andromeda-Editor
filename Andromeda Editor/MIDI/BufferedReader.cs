using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.MIDI
{
    public class BufferedReader : IDisposable
    {
        long pos;
        int buffersize;
        int bufferpos;
        int maxbufferpos;
        long streamstart;
        long streamlen;
        Stream stream;
        byte[] buffer;
        byte[] bufferNext;
        Task? nextReader = null;

        Queue<byte> pushpush = new Queue<byte>();

        /// <summary>
        /// Initializes <c>BufferedReader.</c>
        /// </summary>
        /// <param name="stream">The stream to take from.</param>
        /// <param name="buffersize">The size of the buffer.</param>
        /// <param name="streamstart">Offset of the stream to start the buffer at.</param>
        /// <param name="streamlen">The overall length of the stream.</param>
        public BufferedReader(Stream stream, int buffersize, long streamstart, long streamlen)
        {
            // trim buffersize if it's larger than the real stream length
            if (buffersize > streamlen) buffersize = (int)streamlen;
            this.buffersize = buffersize;
            this.streamstart = streamstart;
            this.streamlen = streamlen;
            this.stream = stream;

            buffer = new byte[buffersize];
            bufferNext = new byte[buffersize];

            UpdateBuffer(pos, true);
        }

        void UpdateBuffer(long pos, bool first = false)
        {
            if (first)
            {
                nextReader = Task.Run(() =>
                {
                    lock (stream)
                    {
                        stream.Position = pos + streamstart;
                        stream.Read(bufferNext, 0, buffersize);
                    }
                });
            }

            nextReader.GetAwaiter().GetResult();
            Buffer.BlockCopy(bufferNext, 0, buffer, 0, buffersize);
            nextReader = Task.Run(() =>
            {
                lock (stream)
                {
                    stream.Position = pos + streamstart + buffersize;
                    stream.Read(bufferNext, 0, buffersize);
                }
            });
            nextReader.GetAwaiter().GetResult();
            maxbufferpos = (int)Math.Min(streamlen - pos + 1, buffersize);
        }

        public long Location => pos + bufferpos;
        public long Length => streamlen;

        public int Pushback = -1;

        public byte ReadByte()
        {
            if (Pushback != -1)
            {
                byte _b = (byte)Pushback;
                if (pushpush.Count > 1) Pushback = pushpush.Dequeue();
                else Pushback = -1;
                return _b;
            }

            byte b = buffer[bufferpos++];
            if (bufferpos < maxbufferpos) return b;
            else if (bufferpos >= buffersize)
            {
                pos += bufferpos;
                bufferpos = 0;
                UpdateBuffer(pos);
                return b;
            }
            else throw new IndexOutOfRangeException();
        }

        public byte ReadFast()
        {
            byte b = buffer[bufferpos++];
            if (bufferpos < maxbufferpos) return b;
            else if (bufferpos >= buffersize)
            {
                pos += bufferpos;
                bufferpos = 0;
                UpdateBuffer(pos);
                return b;
            }
            else throw new IndexOutOfRangeException();
        }

        public void Reset()
        {
            pos = 0;
            bufferpos = 0;
            UpdateBuffer(pos, true);
        }

        public void Skip(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Pushback != -1)
                {
                    if (pushpush.Count > 1) Pushback = pushpush.Dequeue();
                    else Pushback = -1;
                    continue;
                }
                bufferpos++;
                if (bufferpos < maxbufferpos) continue;
                if (bufferpos >= buffersize)
                {
                    pos += bufferpos;
                    bufferpos = 0;
                    UpdateBuffer(pos);
                }
                else throw new IndexOutOfRangeException();
            }
        }

        public void PushToQueue(byte b)
        {
            if (Pushback == -1) Pushback = b;
            else pushpush.Enqueue(b);
        }

        public void Dispose()
        {
            buffer = null;
            bufferNext = null;
            pushpush.Clear();
            nextReader = null;
        }
    }
}
