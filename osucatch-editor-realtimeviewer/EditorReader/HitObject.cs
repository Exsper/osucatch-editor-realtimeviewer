﻿// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using System;
using System.Globalization;
using osucatch_editor_realtimeviewer;


namespace Editor_Reader;

public class HitObject
{
    public double SpatialLength;

    public int StartTime;

    public int EndTime;

    public int Type;

    public int SoundType;

    public int SegmentCount;

    public float X;

    public float Y;

    public float BaseX;

    public float BaseY;

    public string SampleFile;

    public int SampleVolume;

    public int SampleSet;

    public int SampleSetAdditions;

    public int CustomSampleSet;

    public int[] SoundTypeList;

    public int[] SampleSetList;

    public int[] SampleSetAdditionsList;

    public bool IsSelected;

    public bool unifiedSoundAddition;

    private static char[] CurveChar = new char[4] { 'C', 'B', 'L', 'P' };

    public int CurveType;

    public float X2;

    public float Y2;

    public float[] sliderCurvePoints;

    public double curveLength;

    public void DeStack()
    {
        float num = X - BaseX;
        float num2 = Y - BaseY;
        if (num == 0f && num2 == 0f)
        {
            return;
        }

        X -= num;
        Y -= num2;
        if (IsSlider())
        {
            X2 -= num;
            Y2 -= num2;
            for (int i = 0; i < sliderCurvePoints.Length; i += 2)
            {
                sliderCurvePoints[i] -= num;
                sliderCurvePoints[i + 1] -= num2;
            }
        }
    }

    public void Round()
    {
        X = (float)Math.Floor((double)X + 0.5);
        Y = (float)Math.Floor((double)Y + 0.5);
        if (IsSlider())
        {
            for (int i = 0; i < sliderCurvePoints.Length; i++)
            {
                sliderCurvePoints[i] = (float)Math.Floor((double)sliderCurvePoints[i] + 0.5);
            }
        }
    }

    public bool IsCircle()
    {
        return (Type & 1) > 0;
    }

    public bool IsSlider()
    {
        return (Type & 2) > 0;
    }

    public bool IsNewCombo()
    {
        return (Type & 4) > 0;
    }

    public bool IsSpinner()
    {
        return (Type & 8) > 0;
    }

    public bool IsHoldNote()
    {
        return (Type & 0x80) > 0;
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}{5}", X, Y, StartTime, Type, SoundType, Extras());
    }

    private string Extras()
    {
        string text = "";
        if (IsSlider())
        {
            text = string.Format(CultureInfo.InvariantCulture, ",{0}", SliderString());
        }

        if (IsSpinner() || IsHoldNote())
        {
            text = string.Format(CultureInfo.InvariantCulture, ",{0}", EndTime);
        }

        return text + (unifiedSoundAddition ? "" : string.Format(CultureInfo.InvariantCulture, ",{0}:{1}:{2}:{3}:{4}", SampleSet, SampleSetAdditions, CustomSampleSet, SampleVolume, SampleFile));
    }

    private string SliderString()
    {
        return string.Format(CultureInfo.InvariantCulture, "{0}{1},{2},{3}{4}", CurveChar[CurveType], AnchorsString(), SegmentCount, SpatialLength, EdgesString());
    }

    private string AnchorsString()
    {
        string[] array = new string[sliderCurvePoints.Length / 2];
        for (int i = 1; i < array.Length; i++)
        {
            array[i] = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", sliderCurvePoints[2 * i], sliderCurvePoints[2 * i + 1]);
        }

        return string.Join("|", array);
    }

    private string EdgesString()
    {
        if (unifiedSoundAddition)
        {
            return "";
        }

        string[] array = new string[SegmentCount + 1];

        if (SampleSetList.Length <= SegmentCount || SampleSetAdditionsList.Length <= SegmentCount)
        {
            Log.ConsoleLog("Building slider error : SampleSetList.Length=" + SampleSetList.Length + ", SampleSetAdditionsList.Length=" + SampleSetAdditionsList.Length + ", SegmentCount=" + SegmentCount, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new Exception("ReadProcessMemory Error. Cancelled reading.");
        }
        for (int i = 0; i < SegmentCount + 1; i++)
        {
            array[i] = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", SampleSetList[i], SampleSetAdditionsList[i]);
        }

        return string.Format(CultureInfo.InvariantCulture, ",{0},{1}", string.Join("|", SoundTypeList), string.Join("|", array));
    }
}