using Andromeda_Editor.Editor.Settings;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Andromeda_Editor.Editor
{
    public struct TimeSignature
    {
        public double Tick;
        public uint Numerator;
        public uint Denominator;
    }

    public struct Bar
    {
        public double Tick;
        public double Length;
        public uint Numerator;
        public uint Denominator;
        public uint BarNumber;
    }

    public static class TrackBars
    {
        private static List<TimeSignature> signatures = new List<TimeSignature>();
        public static Bar[] cachedBars = new Bar[RenderSettings.BarBufferLength];

        public static double GetTimeSignatureBarLength(uint Numerator, uint Denominator)
        {
            double signatureAspect = Numerator / (double)Denominator;
            return ProjectSettings.PPQ * 4.0 * signatureAspect;
        }

        public static Bar GetBar(uint barNumber)
        {
            double barLengthInTicks = GetTimeSignatureBarLength(ProjectSettings.StartTSNumerator, ProjectSettings.StartTSDenominator);

            // no time signatures; skip the complex calculations
            if (signatures.Count == 0)
            {
                return new Bar
                {
                    Tick = barLengthInTicks * barNumber,
                    Length = barLengthInTicks,
                    Numerator = ProjectSettings.StartTSNumerator,
                    Denominator = ProjectSettings.StartTSDenominator,
                    BarNumber = barNumber
                };
            }

            double currentTick = 0;
            uint currentBar = 0;

            foreach (TimeSignature sig in signatures)
            {
                double signatureTick = sig.Tick;
                double signatureBarLengthInTicks = GetTimeSignatureBarLength(sig.Numerator, sig.Denominator);

                while (currentTick + barLengthInTicks <= signatureTick && currentBar < barNumber)
                {
                    currentTick += barLengthInTicks;
                    currentBar++;
                }

                if (currentBar == barNumber)
                {
                    return new Bar()
                    {
                        Tick = currentTick,
                        Length = signatureBarLengthInTicks,
                        Numerator = sig.Numerator,
                        Denominator = sig.Denominator,
                        BarNumber = barNumber,
                    };
                }

                barLengthInTicks = signatureBarLengthInTicks;
                currentTick = signatureTick;
                currentBar++;
            }

            while (currentBar < barNumber)
            {
                currentTick += barLengthInTicks;
                currentBar++;
            }

            return new Bar()
            {
                Tick = currentTick,
                Length = GetTimeSignatureBarLength(signatures.Last().Numerator, signatures.Last().Denominator),
                Numerator = signatures.Last().Numerator,
                Denominator = signatures.Last().Denominator,
                BarNumber = barNumber
            };
        }

        public static void AddTimeSignature(TimeSignature timeSignature)
        {
            int index = Utils.BinarySearch(signatures, (int)timeSignature.Tick, idx => (int)signatures[idx].Tick);
            signatures.Insert(index, timeSignature);
            UpdateBarTicks();
        }

        // To refresh the bar tick position cache
        public static void UpdateBarTicks()
        {
            for (int i = 0; i < RenderSettings.BarBufferLength; i++)
            {
                cachedBars[i] = GetBar((uint)i);
            }
        }
    }

}