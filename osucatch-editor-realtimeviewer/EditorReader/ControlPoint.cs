﻿// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using System.Globalization;

namespace Editor_Reader;

public class ControlPoint
{
    public double BeatLength;

    public double Offset;

    public int CustomSamples;

    public int SampleSet;

    public int TimeSignature;

    public int Volume;

    public int EffectFlags;

    public bool TimingChange;

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5},{6},{7}", Offset, BeatLength, TimeSignature, SampleSet, CustomSamples, Volume, TimingChange ? 1 : 0, EffectFlags);
    }
}