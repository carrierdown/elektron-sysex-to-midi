using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MDSysExToMidi
{
    class Pattern
    {
        public Track[] Tracks { get; set; } = new Track[16];
    }

    class Track
    {
        public bool[] Trigs { get; set; } = new bool[64];
    }

    class Program
    {
        private static byte[] PatternHeader = new byte[] { 0xF0, 0x00, 0x20, 0x3C, 0x02, 0x00, 0x67, 0x03, 0x01 };

        // MIDI header info for simple 1-track 96 PPQ file
        private static byte[] MidiHeaderBytes = new byte[] {
            /* header chunk id */       0x4D, 0x54, 0x68, 0x64, 
            /* header chunk size */     0x00, 0x00, 0x00, 0x06,
            /* format type */           0x00, 0x00,
            /* number of tracks */      0x00, 0x01,
            /* time division */         0x00, 0x60,
            /* track chunk id */        0x4D, 0x54, 0x72, 0x6B
        };

        struct NoteEvent
        {
            public int DeltaTicks;
            public EventType Type;
            public byte Pitch;
            public byte Velocity;
        }

        // midi channel 0 assumed
        enum EventType
        {
            NoteOn = 0x90,
            NoteOff = 0x80
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("No arguments given. Usage: MDSysExToMidi filename.syx");
                return;
            }
            string filename = args[0];
            if (!File.Exists(filename))
            {
                Console.WriteLine($@"File {filename} was not found. Please specify a valid path and filename, e.g. c:\temp\mysysexfile.syx");
                return;
            }

            byte[] fileBytes = File.ReadAllBytes(filename);
            int i = 0,
                midiFileNumber = 0;

            Console.WriteLine($"Parsing file {filename}");

            while (i < fileBytes.Length)
            {
                var ix = SeekSysexSequence(i, PatternHeader, fileBytes);
                if (ix > 0)
                {
                    int seqStartIndex = ix - PatternHeader.Length;

                    byte[] trigPatternBytes = GetDecodedBytes(seqStartIndex + 0x0A, 74, fileBytes);
                    byte[] lockPatternBytes = GetDecodedBytes(seqStartIndex + 0x54, 74, fileBytes);
                    byte[] accentPatternBytes = GetDecodedBytes(seqStartIndex + 0x9E, 19, fileBytes);
                    byte accentAmount = fileBytes[seqStartIndex + 0xB1];
                    byte patternLength = fileBytes[seqStartIndex + 0xB2];
                    byte tempoMultiplier = fileBytes[seqStartIndex + 0xB3];
                    byte scale = fileBytes[seqStartIndex + 0xB4];
                    byte kit = fileBytes[seqStartIndex + 0xB5];
                    byte[] lockBytes = GetDecodedBytes(seqStartIndex + 0xB7, 2341, fileBytes);
                    byte[] extraPatternBytes = GetDecodedBytes(seqStartIndex + 0x9DC, 234, fileBytes);
                    byte[] extraPattern64Bytes = GetDecodedBytes(seqStartIndex + 0xAC6, 2647, fileBytes);

                    bool check = (fileBytes[seqStartIndex + 0x1521] == 0xf7);
                    Console.WriteLine($"Wrote output-{midiFileNumber}.mid {(check ? "successfully" : "with errors")}");

                    Pattern pattern = GetPatternFromBytes(trigPatternBytes);
                    AddExtraPattern64(pattern, extraPattern64Bytes);
                    PatternToMidiFile($"output-{midiFileNumber++}.mid", pattern);

                    ix = seqStartIndex + 0x1521;
                    i = ix;
                }
                i++;
            }
        }

        static Pattern GetPatternFromBytes(byte[] trigPatternBytes)
        {
            Pattern pattern = new Pattern();

            int byteIndex = 0,
                trackIx = 0;
            while (byteIndex < trigPatternBytes.Length)
            {
                pattern.Tracks[trackIx++] = GetTrackFromBytes(trigPatternBytes, byteIndex);
                byteIndex += 4;
            }

            return pattern;
        }

        static void AddExtraPattern64(Pattern pattern, byte[] extraPatternBytes)
        {
            int byteIndex = 0,
                trackIx = 0;
            while (byteIndex < 64)
            {
                var byteCountInverse = 3;
                var byteCount = 0;
                for (var i = byteIndex; i < byteIndex + 4; i++)
                {
                    byte currentByte = extraPatternBytes[byteIndex + byteCountInverse--];
                    for (var b = 0; b < 8; b++)
                    {
                        pattern.Tracks[trackIx].Trigs[32 + (byteCount * 8) + b] = (currentByte & (1 << b)) > 0;
                    }
                    byteCount++;
                }
                byteIndex += 4;
                trackIx++;
            }
        }

        static void PatternToMidiFile(string filename, Pattern pattern)
        {
            var midiBytes = new List<byte>();
            int deltaTicks = 0;
            int rootNote = 36;
            bool firstMidiEvent = true;
            for (var trigIx = 0; trigIx < 64; trigIx++)
            {
                var noteOns = new List<NoteEvent>();
                for (var trackIx = 0; trackIx < pattern.Tracks.Length; trackIx++)
                {
                    if (pattern.Tracks[trackIx].Trigs[trigIx])
                    {
                        noteOns.Add(new NoteEvent { DeltaTicks = deltaTicks, Pitch = (byte)(rootNote + trackIx), Type = EventType.NoteOn, Velocity = 127 });
                        deltaTicks = 0;
                    }
                }
                if (noteOns.Count > 0)
                {
                    var noteOffs = new List<NoteEvent>();
                    var i = 0;
                    foreach (var noteOn in noteOns)
                    {
                        noteOffs.Add(new NoteEvent { DeltaTicks = (i++ == 0 ? 0x18 : 0), Pitch = noteOn.Pitch, Type = EventType.NoteOff, Velocity = 0 });
                        AppendVariableLengthValue(noteOn.DeltaTicks, midiBytes);
                        // Utilize "running status" by only specifying the event type the first time
                        if (firstMidiEvent)
                        {
                            midiBytes.Add((byte)noteOn.Type);
                            firstMidiEvent = false;
                        }
                        midiBytes.Add(noteOn.Pitch);
                        midiBytes.Add(noteOn.Velocity);
                    }
                    foreach (var noteOff in noteOffs)
                    {
                        midiBytes.Add((byte)noteOff.DeltaTicks);
                        midiBytes.Add(noteOff.Pitch);
                        midiBytes.Add(noteOff.Velocity);
                    }
                }
                else
                {
                    deltaTicks += 0x18; // 1/16th note
                }
            }
            // end of track
            midiBytes.Add(0);
            midiBytes.Add(0xFF);
            midiBytes.Add(0x2F);
            midiBytes.Add(0);
            byte[] lengthBytes = new byte[4];
            int count = midiBytes.Count;
            lengthBytes[0] = (byte)((count & 0xff000000) >> 24);
            lengthBytes[1] = (byte)((count & 0x00ff0000) >> 16);
            lengthBytes[2] = (byte)((count & 0x0000ff00) >> 8);
            lengthBytes[3] = (byte)(count & 0x000000ff);

            byte[] bytesToWrite = MidiHeaderBytes.Concat(lengthBytes).Concat(midiBytes).ToArray();
            File.WriteAllBytes($@"c:\temp\{filename}", bytesToWrite);
        }

        static void AppendVariableLengthValue(int value, List<byte> bytes)
        {
            var encodedBytes = new List<byte>();
            if (value < 128)
            {
                bytes.Add((byte)value);
                return;
            }
            var i = 0;
            while (value > 0)
            {
                var encodedByte = (byte)(value & 0b0111_1111);
                if (i++ > 0)
                {
                    encodedByte |= 0b1000_0000;
                }
                encodedBytes.Add(encodedByte);
                value >>= 7;
            }
            encodedBytes.Reverse();
            bytes.AddRange(encodedBytes);
        }

        static Track GetTrackFromBytes(byte[] trigPatternBytes, int offset)
        {
            Track track = new Track();
            var byteCountInverse = 3;
            var byteCount = 0;
            for (var i = offset; i < offset + 4; i++)
            {
                byte currentByte = trigPatternBytes[offset + byteCountInverse--];
                for (var b = 0; b < 8; b++)
                {
                    track.Trigs[(byteCount * 8) + b] = (currentByte & (1 << b)) > 0;
                }
                byteCount++;
            }
            return track;
        }

        static byte[] GetDecodedBytes(int startPos, int length, byte[] fileBytes)
        {
            int ix = startPos;
            int end = ix + length;
            byte[] resultBytes = new byte[DataLengthToByteLength(length)];
            int decodedIx = 0;
            while (ix < end)
            {
                byte[] bytes = ProcessEncodedChunk(ref ix, end, fileBytes);
                foreach (var @byte in bytes)
                {
                    resultBytes[decodedIx++] = @byte;
                }
            }
            return resultBytes;
        }

        static int DataLengthToByteLength(int dataLength)
        {
            return ((dataLength / 8) * 7) + ((dataLength % 8) > 0 ? (dataLength % 8) - 1 : 0);
        }

        static int SeekSysexSequence(int startPos, byte[] searchValues, byte[] haystack)
        {
            var i = startPos;
            int seekPos = 0;
            while (i < haystack.Length)
            {
                while (seekPos < searchValues.Length && searchValues[seekPos++] == haystack[i++])
                {
                    //Console.WriteLine("found entry " + haystack[i - 1]);
                    if (seekPos >= searchValues.Length)
                    {
                        return i;
                    }
                }
                seekPos = 0;
            }
            return -1;
        }

        static byte[] ProcessEncodedChunk(ref int ix, int end, byte[] fileBytes)
        {
            var length = 8;
            if (ix + 8 >= end)
            {
                length = end - ix;
            }
            if (length < 2) { return new byte[0]; }
            byte[] result = new byte[length - 1];
            byte msbs = fileBytes[ix++];
            byte bitMask = 0b0100_0000;
            var bitMaskCorrection = 1;
            for (int i = 0; i < result.Length; i++)
            {
                byte partial = fileBytes[ix++];
                result[i] = (byte)((msbs & bitMask) << bitMaskCorrection);
                result[i] |= partial;
                bitMaskCorrection++;
                bitMask >>= 1;
            }
            return result;
        }
    }
}
