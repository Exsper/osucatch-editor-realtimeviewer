﻿// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Editor_Reader;

public class EditorReader
{
    public bool autoDeStack = true;

    public bool autoRound;

    private byte[] buffer;

    private byte[] buffer4 = new byte[4];

    private byte[] buffer16 = new byte[16];

    private byte[] bufferCp = new byte[48];

    private byte[] bufferOb = new byte[336];

    private IntPtr bytesRead;

    private Process process;

    private IntPtr pEditor;

    private IntPtr pCompose;

    private IntPtr pHOM;

    private IntPtr pBeatmap;

    public float objectRadius;

    public float stackOffset;

    private IntPtr pBookmarksL;

    private IntPtr pBookmarksA;

    public int numBookmarks;

    public int[] bookmarks;

    public string ContainingFolder;

    public string Filename;

    public float HPDrainRate;

    public float CircleSize;

    public float OverallDifficulty;

    public float ApproachRate;

    public double SliderMultiplier;

    public double SliderTickRate;

    public int PreviewTime;

    public float StackLeniency;

    public float TimelineZoom;

    private IntPtr pControlPointsL;

    private IntPtr pControlPointsA;

    public int numControlPoints;

    private byte[] pControlPoints;

    public List<ControlPoint> controlPoints;

    private IntPtr pObjectsL;

    private IntPtr pObjectsA;

    public int numObjects;

    private byte[] pObjects;

    public List<HitObject> hitObjects;

    private IntPtr pClipboardL;

    private IntPtr pClipboardA;

    public int numClipboard;

    private byte[] pClipboard;

    public List<HitObject> clipboardObjects;

    private IntPtr pSelectedL;

    private IntPtr pSelectedA;

    public int numSelected;

    private byte[] pSelected;

    public List<HitObject> selectedObjects;

    private IntPtr pHoveredObject;

    public HitObject hoveredObject;

    private IntPtr pSliderPlacement;

    public HitObject sliderPlacement;

    private IntPtr pPointsL;

    private IntPtr pSTL;

    private IntPtr pSSL;

    private IntPtr pSSAL;

    private IntPtr pTempA;

    private int numTemp;

    private byte[] bTemp;

    [DllImport("kernel32.dll", SetLastError = true)]
    protected static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref IntPtr lpNumberOfBytesRead);

    private string ReadString(IntPtr pString)
    {
        if (pString == IntPtr.Zero)
        {
            return null;
        }

        ReadProcessMemory(process.Handle, pString + 4, buffer4, 4, ref bytesRead);
        int num = BitConverter.ToInt32(buffer4, 0);
        byte[] array = new byte[2 * num];
        ReadProcessMemory(process.Handle, pString + 8, array, 2 * num, ref bytesRead);
        char[] array2 = new char[num];
        Buffer.BlockCopy(array, 0, array2, 0, 2 * num);
        return new string(array2);
    }

    private static byte[] ToByteArray(string hexString)
    {
        byte[] array = new byte[hexString.Length / 2];
        for (int i = 0; i < hexString.Length - 1; i += 2)
        {
            array[i / 2] = Convert.ToByte(hexString.Substring(i, 2), 16);
        }

        return array;
    }

    private static bool PatternCheck(byte[] searchBuffer, byte[] arrPattern, int nOffset)
    {
        if (nOffset + arrPattern.Length > searchBuffer.Length)
        {
            return false;
        }

        for (int i = 0; i < arrPattern.Length; i++)
        {
            if (arrPattern[i] != 238 && arrPattern[i] != searchBuffer[nOffset + i])
            {
                return false;
            }
        }

        return true;
    }

    private IntPtr FindEditorAddress()
    {
        Internals internals = new Internals();
        internals.MemInfo(process.Handle);
        byte[] array = ToByteArray("230000001400000019000000eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee0C000000eeeeeeeeeeeeeeeeeeeeeeeeee00");
        for (int i = 0; i < internals.MemReg.Count; i++)
        {
            Internals.MEMORY_BASIC_INFORMATION mEMORY_BASIC_INFORMATION = internals.MemReg[i];
            buffer = new byte[(int)mEMORY_BASIC_INFORMATION.RegionSize];
            if (!ReadProcessMemory(process.Handle, mEMORY_BASIC_INFORMATION.BaseAddress, buffer, (int)mEMORY_BASIC_INFORMATION.RegionSize, ref bytesRead))
            {
                continue;
            }

            if (mEMORY_BASIC_INFORMATION.RegionSize != bytesRead)
            {
                Array.Resize(ref buffer, (int)bytesRead);
            }

            for (int j = 0; j <= buffer.Length - array.Length; j += 4)
            {
                if (PatternCheck(buffer, array, j) && !EditorMissingObjects(mEMORY_BASIC_INFORMATION.BaseAddress + j - 160))
                {
                    return mEMORY_BASIC_INFORMATION.BaseAddress + j - 160;
                }
            }
        }

        throw new InvalidOperationException("No active editor found.");
    }

    public void SetProcess(Process forceProcess = null)
    {
        if (forceProcess != null)
        {
            this.process = forceProcess;
            return;
        }

        Process[] processesByName = Process.GetProcessesByName("osu!");
        foreach (Process process in processesByName)
        {
            if (process.MainModule.ModuleName == "osu!.exe" && process.MainModule.FileVersionInfo.ProductName == "osu!")
            {
                this.process = process;
                return;
            }
        }

        throw new InvalidOperationException("No process for osu!.exe found.");
    }

    public bool ProcessNeedsReload()
    {
        if (process != null)
        {
            return process.HasExited;
        }

        return true;
    }

    public string ProcessTitle()
    {
        if (ProcessNeedsReload())
        {
            return "";
        }

        process.Refresh();
        return process.MainWindowTitle;
    }

    private IntPtr ToIntPtr(byte[] value, int startIndex)
    {
        if (IntPtr.Size > 4)
        {
            return (IntPtr)BitConverter.ToUInt32(value, startIndex);
        }

        return (IntPtr)BitConverter.ToInt32(value, startIndex);
    }

    public void SetEditor()
    {
        pEditor = FindEditorAddress();
    }

    public bool EditorNeedsReload()
    {
        if (ProcessNeedsReload())
        {
            return true;
        }

        ReadProcessMemory(process.Handle, pEditor + 160, buffer16, 16, ref bytesRead);
        ReadProcessMemory(process.Handle, pEditor + 208, buffer4, 4, ref bytesRead);
        if (pEditor == IntPtr.Zero || BitConverter.ToBoolean(buffer4, 1) || BitConverter.ToInt32(buffer16, 0) != 35 || BitConverter.ToInt32(buffer16, 4) != 20 || BitConverter.ToInt32(buffer16, 8) != 25)
        {
            return true;
        }

        return EditorMissingObjects(pEditor);
    }

    private bool EditorMissingObjects(IntPtr pE)
    {
        ReadProcessMemory(process.Handle, pE + 28, buffer4, 4, ref bytesRead);
        IntPtr intPtr = ToIntPtr(buffer4, 0);
        ReadProcessMemory(process.Handle, intPtr + 72, buffer4, 4, ref bytesRead);
        IntPtr intPtr2 = ToIntPtr(buffer4, 0);
        if (!(intPtr == IntPtr.Zero))
        {
            return intPtr2 == IntPtr.Zero;
        }

        return true;
    }

    public int BeatDivisor()
    {
        ReadProcessMemory(process.Handle, pEditor + 172, buffer4, 4, ref bytesRead);
        return BitConverter.ToInt32(buffer4, 0);
    }

    public int EditorTime()
    {
        ReadProcessMemory(process.Handle, pEditor + 176, buffer16, 16, ref bytesRead);
        return (BitConverter.ToInt32(buffer16, 8) + BitConverter.ToInt32(buffer16, 12)) / 2;
    }

    public void SetHOM()
    {
        ReadProcessMemory(process.Handle, pEditor + 28, buffer4, 4, ref bytesRead);
        pHOM = ToIntPtr(buffer4, 0);
        ReadProcessMemory(process.Handle, pEditor + 112, buffer4, 4, ref bytesRead);
        pCompose = ToIntPtr(buffer4, 0);
    }

    public void ReadHOM()
    {
        buffer = new byte[80];
        ReadProcessMemory(process.Handle, pHOM, buffer, 80, ref bytesRead);
        objectRadius = BitConverter.ToSingle(buffer, 24);
        stackOffset = BitConverter.ToSingle(buffer, 44);
        pBookmarksL = ToIntPtr(buffer, 56);
        pObjectsL = ToIntPtr(buffer, 72);
        buffer = new byte[256];
        ReadProcessMemory(process.Handle, pCompose, buffer, 256, ref bytesRead);
        pClipboardL = ToIntPtr(buffer, 48);
        pSelectedL = ToIntPtr(buffer, 72);
    }

    public int GridSize()
    {
        ReadProcessMemory(process.Handle, pCompose + 20, buffer4, 4, ref bytesRead);
        IntPtr intPtr = ToIntPtr(buffer4, 0);
        ReadProcessMemory(process.Handle, intPtr + 12, buffer4, 4, ref bytesRead);
        return BitConverter.ToInt32(buffer4, 0);
    }

    public double DistanceSpacing()
    {
        ReadProcessMemory(process.Handle, pCompose + 20, buffer16, 16, ref bytesRead);
        return BitConverter.ToDouble(buffer16, 8);
    }

    public int ComposeTool()
    {
        ReadProcessMemory(process.Handle, pCompose + 156, buffer4, 4, ref bytesRead);
        return BitConverter.ToInt32(buffer4, 0);
    }

    public Tuple<float, float> SnapPosition()
    {
        ReadProcessMemory(process.Handle, pCompose + 228, buffer16, 16, ref bytesRead);
        return new Tuple<float, float>(BitConverter.ToSingle(buffer16, 8), BitConverter.ToSingle(buffer16, 12));
    }

    public void FetchBookmarks()
    {
        ReadProcessMemory(process.Handle, pBookmarksL, buffer16, 16, ref bytesRead);
        pBookmarksA = ToIntPtr(buffer16, 4);
        numBookmarks = BitConverter.ToInt32(buffer16, 12);
        buffer = new byte[4 * numBookmarks];
        bookmarks = new int[numBookmarks];
        ReadProcessMemory(process.Handle, pBookmarksA + 8, buffer, 4 * numBookmarks, ref bytesRead);
        Buffer.BlockCopy(buffer, 0, bookmarks, 0, 4 * numBookmarks);
    }

    public void SetBeatmap()
    {
        ReadProcessMemory(process.Handle, pHOM + 48, buffer4, 4, ref bytesRead);
        pBeatmap = ToIntPtr(buffer4, 0);
    }

    public void ReadBeatmap()
    {
        buffer = new byte[320];
        ReadProcessMemory(process.Handle, pBeatmap, buffer, 320, ref bytesRead);
        SliderMultiplier = BitConverter.ToDouble(buffer, 8);
        SliderTickRate = BitConverter.ToDouble(buffer, 16);
        ApproachRate = BitConverter.ToSingle(buffer, 44);
        CircleSize = BitConverter.ToSingle(buffer, 48);
        HPDrainRate = BitConverter.ToSingle(buffer, 52);
        OverallDifficulty = BitConverter.ToSingle(buffer, 56);
        ContainingFolder = ReadString(ToIntPtr(buffer, 120));
        Filename = ReadString(ToIntPtr(buffer, 144));
        PreviewTime = BitConverter.ToInt32(buffer, 288);
        StackLeniency = BitConverter.ToSingle(buffer, 296);
        TimelineZoom = BitConverter.ToSingle(buffer, 304);
    }

    public void SetControlPoints()
    {
        buffer = new byte[192];
        ReadProcessMemory(process.Handle, pBeatmap, buffer, 192, ref bytesRead);
        pControlPointsL = ToIntPtr(buffer, 176);
        ReadProcessMemory(process.Handle, pControlPointsL, buffer16, 16, ref bytesRead);
        pControlPointsA = ToIntPtr(buffer16, 4);
        numControlPoints = BitConverter.ToInt32(buffer16, 12);
        pControlPoints = new byte[4 * numControlPoints];
        ReadProcessMemory(process.Handle, pControlPointsA + 8, pControlPoints, 4 * numControlPoints, ref bytesRead);
    }

    public void ReadControlPoints()
    {
        controlPoints = new List<ControlPoint>();
        for (int i = 0; i < numControlPoints; i++)
        {
            controlPoints.Add(ReadControlPoint(ToIntPtr(pControlPoints, 4 * i)));
        }
    }

    private ControlPoint ReadControlPoint(IntPtr pControlPoint)
    {
        ReadProcessMemory(process.Handle, pControlPoint, bufferCp, 48, ref bytesRead);
        return new ControlPoint
        {
            BeatLength = BitConverter.ToDouble(bufferCp, 4),
            Offset = BitConverter.ToDouble(bufferCp, 12),
            CustomSamples = BitConverter.ToInt32(bufferCp, 20),
            SampleSet = BitConverter.ToInt32(bufferCp, 24),
            TimeSignature = BitConverter.ToInt32(bufferCp, 28),
            Volume = BitConverter.ToInt32(bufferCp, 32),
            EffectFlags = BitConverter.ToInt32(bufferCp, 36),
            TimingChange = BitConverter.ToBoolean(bufferCp, 40)
        };
    }

    public void SetObjects()
    {
        ReadProcessMemory(process.Handle, pObjectsL, buffer16, 16, ref bytesRead);
        pObjectsA = ToIntPtr(buffer16, 4);
        numObjects = BitConverter.ToInt32(buffer16, 12);
        pObjects = new byte[4 * numObjects];
        ReadProcessMemory(process.Handle, pObjectsA + 8, pObjects, 4 * numObjects, ref bytesRead);
    }

    public void ReadObjects()
    {
        hitObjects = new List<HitObject>();
        for (int i = 0; i < numObjects; i++)
        {
            hitObjects.Add(ReadObject(ToIntPtr(pObjects, 4 * i)));
        }
    }

    public void SetClipboard()
    {
        ReadProcessMemory(process.Handle, pClipboardL, buffer16, 16, ref bytesRead);
        pClipboardA = ToIntPtr(buffer16, 4);
        numClipboard = BitConverter.ToInt32(buffer16, 12);
        pClipboard = new byte[4 * numClipboard];
        ReadProcessMemory(process.Handle, pClipboardA + 8, pClipboard, 4 * numClipboard, ref bytesRead);
    }

    public void ReadClipboard()
    {
        clipboardObjects = new List<HitObject>();
        for (int i = 0; i < numClipboard; i++)
        {
            clipboardObjects.Add(ReadObject(ToIntPtr(pClipboard, 4 * i)));
        }
    }

    public void SetSelected()
    {
        ReadProcessMemory(process.Handle, pSelectedL, buffer16, 16, ref bytesRead);
        pSelectedA = ToIntPtr(buffer16, 4);
        numSelected = BitConverter.ToInt32(buffer16, 12);
        pSelected = new byte[4 * numSelected];
        ReadProcessMemory(process.Handle, pSelectedA + 8, pSelected, 4 * numSelected, ref bytesRead);
    }

    public void ReadSelected()
    {
        selectedObjects = new List<HitObject>();
        for (int i = 0; i < numSelected; i++)
        {
            selectedObjects.Add(ReadObject(ToIntPtr(pSelected, 4 * i)));
        }
    }

    public void SetHovered()
    {
        ReadProcessMemory(process.Handle, pCompose + 64, buffer4, 4, ref bytesRead);
        pHoveredObject = ToIntPtr(buffer4, 0);
    }

    public bool ReadHovered()
    {
        if (pHoveredObject == IntPtr.Zero)
        {
            hoveredObject = null;
            return false;
        }

        hoveredObject = ReadObject(pHoveredObject);
        return true;
    }

    public void SetSliderPlacement()
    {
        ReadProcessMemory(process.Handle, pCompose + 84, buffer4, 4, ref bytesRead);
        pSliderPlacement = ToIntPtr(buffer4, 0);
    }

    public bool ReadSliderPlacement()
    {
        if (pSliderPlacement == IntPtr.Zero)
        {
            sliderPlacement = null;
            return false;
        }

        sliderPlacement = ReadObject(pSliderPlacement);
        return true;
    }

    private HitObject ReadObject(IntPtr pObject)
    {
        ReadProcessMemory(process.Handle, pObject, bufferOb, 336, ref bytesRead);
        HitObject hitObject = new HitObject();
        hitObject.SpatialLength = BitConverter.ToDouble(bufferOb, 8);
        hitObject.StartTime = BitConverter.ToInt32(bufferOb, 16);
        hitObject.EndTime = BitConverter.ToInt32(bufferOb, 20);
        hitObject.Type = BitConverter.ToInt32(bufferOb, 24);
        hitObject.SoundType = BitConverter.ToInt32(bufferOb, 28);
        hitObject.SegmentCount = BitConverter.ToInt32(bufferOb, 32);
        hitObject.X = BitConverter.ToSingle(bufferOb, 56);
        hitObject.Y = BitConverter.ToSingle(bufferOb, 60);
        hitObject.SampleFile = ReadString(ToIntPtr(bufferOb, 84));
        hitObject.Type |= (BitConverter.ToInt32(bufferOb, 96) & 7) << 4;
        hitObject.SampleVolume = BitConverter.ToInt32(bufferOb, 108);
        hitObject.SampleSet = BitConverter.ToInt32(bufferOb, 112);
        hitObject.SampleSetAdditions = BitConverter.ToInt32(bufferOb, 116);
        hitObject.CustomSampleSet = BitConverter.ToInt32(bufferOb, 120);
        hitObject.IsSelected = BitConverter.ToBoolean(bufferOb, 133);
        hitObject.BaseX = BitConverter.ToSingle(bufferOb, 140);
        hitObject.BaseY = BitConverter.ToSingle(bufferOb, 144);
        hitObject.unifiedSoundAddition = BitConverter.ToBoolean(bufferOb, 302);
        if (hitObject.IsSlider())
        {
            hitObject.curveLength = BitConverter.ToDouble(bufferOb, 148);
            hitObject.CurveType = BitConverter.ToInt32(bufferOb, 264);
            hitObject.X2 = BitConverter.ToSingle(bufferOb, 320);
            hitObject.Y2 = BitConverter.ToSingle(bufferOb, 324);
            pPointsL = ToIntPtr(bufferOb, 212);
            pSTL = ToIntPtr(bufferOb, 240);
            pSSL = ToIntPtr(bufferOb, 244);
            pSSAL = ToIntPtr(bufferOb, 248);
            ReadProcessMemory(process.Handle, pPointsL, buffer16, 16, ref bytesRead);
            pTempA = ToIntPtr(buffer16, 4);
            numTemp = BitConverter.ToInt32(buffer16, 12);
            bTemp = new byte[8 * numTemp];
            ReadProcessMemory(process.Handle, pTempA + 8, bTemp, 8 * numTemp, ref bytesRead);
            hitObject.sliderCurvePoints = new float[2 * numTemp];
            Buffer.BlockCopy(bTemp, 0, hitObject.sliderCurvePoints, 0, 8 * numTemp);
            if (!hitObject.unifiedSoundAddition)
            {
                ReadProcessMemory(process.Handle, pSTL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = BitConverter.ToInt32(buffer16, 12);
                bTemp = new byte[4 * numTemp];
                ReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SoundTypeList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SoundTypeList, 0, 4 * numTemp);
                ReadProcessMemory(process.Handle, pSSL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = BitConverter.ToInt32(buffer16, 12);
                bTemp = new byte[4 * numTemp];
                ReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetList, 0, 4 * numTemp);
                ReadProcessMemory(process.Handle, pSSAL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = BitConverter.ToInt32(buffer16, 12);
                bTemp = new byte[4 * numTemp];
                ReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetAdditionsList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetAdditionsList, 0, 4 * numTemp);
            }
        }

        if (autoDeStack)
        {
            hitObject.DeStack();
        }

        if (autoRound)
        {
            hitObject.Round();
        }

        return hitObject;
    }

    public void FetchEditor()
    {
        if (ProcessNeedsReload())
        {
            SetProcess();
        }

        SetEditor();
    }

    public void FetchHOM()
    {
        if (EditorNeedsReload())
        {
            FetchEditor();
        }

        SetHOM();
        ReadHOM();
    }

    public void FetchBeatmap()
    {
        SetBeatmap();
        ReadBeatmap();
    }

    public void FetchControlPoints()
    {
        SetControlPoints();
        ReadControlPoints();
    }

    public void FetchObjects()
    {
        SetObjects();
        ReadObjects();
    }

    public void FetchClipboard()
    {
        SetClipboard();
        ReadClipboard();
    }

    public void FetchSelected()
    {
        SetSelected();
        ReadSelected();
    }

    public bool FetchHovered()
    {
        SetHovered();
        return ReadHovered();
    }

    public bool FetchSliderPlacement()
    {
        SetSliderPlacement();
        return ReadSliderPlacement();
    }

    public void FetchAll()
    {
        FetchHOM();
        FetchBeatmap();
        FetchControlPoints();
        FetchObjects();
        FetchBookmarks();
    }
}