#nullable enable

using System;
using System.Collections.Generic;

namespace McpTest.Bowling
{
    public readonly struct BowlingScorecard
    {
        public BowlingScorecard(string[] frameMarks, int?[] frameTotals)
        {
            FrameMarks = frameMarks;
            FrameTotals = frameTotals;
        }

        public string[] FrameMarks { get; }

        public int?[] FrameTotals { get; }

        public int TotalScore
        {
            get
            {
                for (var index = FrameTotals.Length - 1; index >= 0; index--)
                {
                    if (FrameTotals[index].HasValue)
                    {
                        return FrameTotals[index]!.Value;
                    }
                }

                return 0;
            }
        }
    }

    public static class BowlingScoreCalculator
    {
        public static BowlingScorecard BuildScorecard(IReadOnlyList<int> rolls)
        {
            if (rolls == null)
            {
                throw new ArgumentNullException(nameof(rolls));
            }

            var frameMarks = new string[10];
            var frameTotals = new int?[10];
            var runningTotal = 0;
            var rollIndex = 0;

            for (var frame = 0; frame < 9; frame++)
            {
                if (rollIndex >= rolls.Count)
                {
                    break;
                }

                var first = ClampPins(rolls[rollIndex]);
                if (first == 10)
                {
                    frameMarks[frame] = "X";
                    if (rollIndex + 2 < rolls.Count)
                    {
                        runningTotal += 10 + ClampPins(rolls[rollIndex + 1]) + ClampPins(rolls[rollIndex + 2]);
                        frameTotals[frame] = runningTotal;
                    }

                    rollIndex++;
                    continue;
                }

                if (rollIndex + 1 >= rolls.Count)
                {
                    frameMarks[frame] = $"{FormatRoll(first)} _";
                    break;
                }

                var second = ClampPins(rolls[rollIndex + 1]);
                var framePins = Math.Min(10, first + second);
                var isSpare = framePins == 10;
                frameMarks[frame] = isSpare
                    ? $"{FormatRoll(first)} /"
                    : $"{FormatRoll(first)} {FormatRoll(second)}";

                if (isSpare)
                {
                    if (rollIndex + 2 < rolls.Count)
                    {
                        runningTotal += 10 + ClampPins(rolls[rollIndex + 2]);
                        frameTotals[frame] = runningTotal;
                    }
                }
                else
                {
                    runningTotal += first + second;
                    frameTotals[frame] = runningTotal;
                }

                rollIndex += 2;
            }

            if (rollIndex < rolls.Count)
            {
                var first = ClampPins(rolls[rollIndex]);
                int? second = rollIndex + 1 < rolls.Count ? ClampPins(rolls[rollIndex + 1]) : null;
                int? third = rollIndex + 2 < rolls.Count ? ClampPins(rolls[rollIndex + 2]) : null;

                frameMarks[9] = FormatTenthFrame(first, second, third);
                if (IsTenthFrameComplete(first, second, third))
                {
                    runningTotal += first + second.GetValueOrDefault() + third.GetValueOrDefault();
                    frameTotals[9] = runningTotal;
                }
            }

            return new BowlingScorecard(frameMarks, frameTotals);
        }

        static string FormatTenthFrame(int first, int? second, int? third)
        {
            var secondMark = second.HasValue ? FormatTenthSecond(first, second.Value) : "_";
            var thirdMark = third.HasValue ? FormatTenthThird(first, second, third.Value) : "_";
            return $"{FormatRoll(first)} {secondMark} {thirdMark}";
        }

        static string FormatTenthSecond(int first, int second)
        {
            if (first == 10)
            {
                return FormatRoll(second);
            }

            return first + second >= 10 ? "/" : FormatRoll(second);
        }

        static string FormatTenthThird(int first, int? second, int third)
        {
            if (!second.HasValue)
            {
                return "_";
            }

            if (first == 10)
            {
                return second.Value == 10
                    ? FormatRoll(third)
                    : second.Value + third >= 10 ? "/" : FormatRoll(third);
            }

            return first + second.Value >= 10 ? FormatRoll(third) : "_";
        }

        static bool IsTenthFrameComplete(int first, int? second, int? third)
        {
            if (!second.HasValue)
            {
                return false;
            }

            if (first == 10 || first + second.Value >= 10)
            {
                return third.HasValue;
            }

            return true;
        }

        static string FormatRoll(int pins)
        {
            return pins switch
            {
                <= 0 => "-",
                10 => "X",
                _ => pins.ToString()
            };
        }

        static int ClampPins(int pins)
        {
            return Math.Clamp(pins, 0, 10);
        }
    }
}
