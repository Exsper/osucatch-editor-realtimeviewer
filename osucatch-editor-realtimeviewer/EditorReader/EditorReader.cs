// Copyright (c) 2019 Karoo13. Licensed under https://github.com/Karoo13/EditorReader/blob/master/LICENSE
// See the LICENCE file in the EditorReader folder for full licence text.
// https://github.com/Karoo13/EditorReader
// Decompiled with ICSharpCode.Decompiler 8.1.1.7464

using osucatch_editor_realtimeviewer;
using System;
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

    public int BeatmapVersion;

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

    /// <summary>
    /// 主物件列表指针 -> 主物件下标，用于轻量地把选中物件列表映射回 SourceIndex。
    /// 每次 SetObjects 时重建。
    /// </summary>
    private Dictionary<IntPtr, int>? masterIndexByPointer;

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

    private static bool SafeReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, ref IntPtr lpNumberOfBytesRead)
    {
        bool result = ReadProcessMemory(hProcess, lpBaseAddress, lpBuffer, dwSize, ref lpNumberOfBytesRead);
        if (!result)
        {
            Log.ConsoleLog("ReadProcessMemory failed: lpBaseAddress: " + lpBaseAddress + ", lpBuffer's length: " + lpBuffer.Length, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new Exception("ReadProcessMemory Error. Cancelled reading.");
        }
        return result;
    }

    private static int SafeBitConverterToInt32(byte[] value, int startIndex, string varName = "")
    {
        const int MAX = 1000000;
        int result = BitConverter.ToInt32(value, startIndex);
        if (result < 0 || result > MAX)
        {
            Log.ConsoleLog("BitConverterToInt32 error: " + varName + "=" + result, Log.LogType.EditorReader, Log.LogLevel.Error);
            throw new Exception("ReadProcessMemory Error. Reading cancelled.");
        }
        return result;
    }

    private static void EnsureBuffer(ref byte[] buf, int size)
    {
        if (buf == null || buf.Length < size)
        {
            buf = new byte[size];
        }
    }

    private string ReadString(IntPtr pString)
    {
        if (pString == IntPtr.Zero)
        {
            return null;
        }

        SafeReadProcessMemory(process.Handle, pString + 4, buffer4, 4, ref bytesRead);
        int num = SafeBitConverterToInt32(buffer4, 0, "ReadString num");
        byte[] array = new byte[2 * num];
        SafeReadProcessMemory(process.Handle, pString + 8, array, 2 * num, ref bytesRead);
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

    /// <summary>
    /// 当前连接的 osu! 进程 ID（用于前台窗口判断，避免每次创建 Process 对象）。
    /// </summary>
    public int? OsuProcessId => process?.Id;

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

    /// <summary>
    /// 清空已缓存的编辑器地址，强制下一次检查时重新扫描内存。
    /// </summary>
    public void ResetEditor()
    {
        pEditor = IntPtr.Zero;
    }

    public bool EditorNeedsReload()
    {
        if (ProcessNeedsReload())
        {
            return true;
        }
        if (pEditor == IntPtr.Zero)
        {
            return true;
        }

        // 读取失败时必须视为需要重载，不能依赖上次成功读取残留的 buffer 值做判断
        // （osu! 从 test mode 退出重建 editor 后，旧 pEditor 可能已失效，ReadProcessMemory 失败但 buffer 仍是旧签名）
        if (!ReadProcessMemory(process.Handle, pEditor + 160, buffer16, 16, ref bytesRead) ||
            !ReadProcessMemory(process.Handle, pEditor + 208, buffer4, 4, ref bytesRead))
        {
            return true;
        }

        if (BitConverter.ToBoolean(buffer4, 1) || BitConverter.ToInt32(buffer16, 0) != 35 || BitConverter.ToInt32(buffer16, 4) != 20 || BitConverter.ToInt32(buffer16, 8) != 25)
        {
            return true;
        }

        return EditorMissingObjects(pEditor);
    }

    private bool EditorMissingObjects(IntPtr pE)
    {
        try
        {
            SafeReadProcessMemory(process.Handle, pE + 28, buffer4, 4, ref bytesRead);
            IntPtr intPtr = ToIntPtr(buffer4, 0);
            if (intPtr == IntPtr.Zero)
            {
                return true;
            }
            SafeReadProcessMemory(process.Handle, intPtr + 72, buffer4, 4, ref bytesRead);
            IntPtr intPtr2 = ToIntPtr(buffer4, 0);
            return intPtr2 == IntPtr.Zero;
        }
        catch
        {
            // 读取失败视为"对象缺失"：EditorNeedsReload 会据此返回 true（需要重载），
            // FindEditorAddress 扫描中则跳过该候选继续扫描，避免假目标中断整次扫描
            return true;
        }
    }

    public int EditorTime()
    {
        SafeReadProcessMemory(process.Handle, pEditor + 176, buffer16, 16, ref bytesRead);
        return (BitConverter.ToInt32(buffer16, 8) + BitConverter.ToInt32(buffer16, 12)) / 2;
    }

    public void SetHOM()
    {
        SafeReadProcessMemory(process.Handle, pEditor + 28, buffer4, 4, ref bytesRead);
        pHOM = ToIntPtr(buffer4, 0);
        SafeReadProcessMemory(process.Handle, pEditor + 112, buffer4, 4, ref bytesRead);
        pCompose = ToIntPtr(buffer4, 0);
    }

    public void ReadHOM()
    {
        EnsureBuffer(ref buffer, 80);
        SafeReadProcessMemory(process.Handle, pHOM, buffer, 80, ref bytesRead);
        objectRadius = BitConverter.ToSingle(buffer, 24);
        stackOffset = BitConverter.ToSingle(buffer, 44);
        pBookmarksL = ToIntPtr(buffer, 56);
        pObjectsL = ToIntPtr(buffer, 72);
        EnsureBuffer(ref buffer, 256);
        SafeReadProcessMemory(process.Handle, pCompose, buffer, 256, ref bytesRead);
        pClipboardL = ToIntPtr(buffer, 48);
        pSelectedL = ToIntPtr(buffer, 72);
    }

    public void FetchBookmarks()
    {
        SafeReadProcessMemory(process.Handle, pBookmarksL, buffer16, 16, ref bytesRead);
        pBookmarksA = ToIntPtr(buffer16, 4);
        numBookmarks = SafeBitConverterToInt32(buffer16, 12, "numBookmarks");
        EnsureBuffer(ref buffer, 4 * numBookmarks);
        bookmarks = new int[numBookmarks];
        SafeReadProcessMemory(process.Handle, pBookmarksA + 8, buffer, 4 * numBookmarks, ref bytesRead);
        Buffer.BlockCopy(buffer, 0, bookmarks, 0, 4 * numBookmarks);
    }

    /// <summary>
    /// 轻量读取当前物件/控制点/书签数量，用于快速判断 beatmap 是否发生变化。
    /// 不会抛异常；指针无效或读取失败时返回 false。
    /// </summary>
    public bool TryReadCounts(out int numObjects, out int numControlPoints, out int numBookmarks)
    {
        numObjects = numControlPoints = numBookmarks = -1;
        if (process == null || pObjectsL == IntPtr.Zero || pControlPointsL == IntPtr.Zero || pBookmarksL == IntPtr.Zero)
        {
            return false;
        }

        bool ok = true;
        ok &= ReadListCount(pObjectsL, out numObjects);
        ok &= ReadListCount(pControlPointsL, out numControlPoints);
        ok &= ReadListCount(pBookmarksL, out numBookmarks);
        return ok;
    }

    /// <summary>
    /// 轻量读取当前选中物件在主物件列表中的下标（0..numObjects-1，顺序不保证与主列表一致）。
    /// 高频 tick 时使用，避免等待 150ms 一次的全量读取。
    /// 不会抛异常；指针无效、读取失败或主物件指针表未就绪时返回 false（调用方沿用旧状态）。
    /// </summary>
    public bool TryReadSelectedIndices(out int[] selectedIndices)
    {
        selectedIndices = Array.Empty<int>();
        if (process == null || pSelectedL == IntPtr.Zero || masterIndexByPointer == null)
        {
            return false;
        }

        if (!ReadProcessMemory(process.Handle, pSelectedL, buffer16, 16, ref bytesRead))
        {
            return false;
        }

        IntPtr pSelA = ToIntPtr(buffer16, 4);
        int selCount = BitConverter.ToInt32(buffer16, 12);
        if (selCount < 0 || selCount > 1000000)
        {
            return false;
        }

        if (selCount == 0)
        {
            return true;
        }

        if (pSelA == IntPtr.Zero)
        {
            return false;
        }

        EnsureBuffer(ref pSelected, 4 * selCount);
        if (!ReadProcessMemory(process.Handle, pSelA + 8, pSelected, 4 * selCount, ref bytesRead))
        {
            return false;
        }

        int[] result = new int[selCount];
        int found = 0;
        for (int i = 0; i < selCount; i++)
        {
            IntPtr objPtr = ToIntPtr(pSelected, 4 * i);
            if (masterIndexByPointer.TryGetValue(objPtr, out int index))
            {
                result[found++] = index;
            }
        }

        if (found != selCount)
        {
            Array.Resize(ref result, found);
        }

        selectedIndices = result;
        return true;
    }

    private bool ReadListCount(IntPtr pList, out int count)
    {
        count = -1;
        if (!ReadProcessMemory(process.Handle, pList, buffer16, 16, ref bytesRead))
        {
            return false;
        }

        int value = BitConverter.ToInt32(buffer16, 12);
        if (value < 0 || value > 1000000)
        {
            return false;
        }

        count = value;
        return true;
    }

    public void SetBeatmap()
    {
        SafeReadProcessMemory(process.Handle, pHOM + 48, buffer4, 4, ref bytesRead);
        pBeatmap = ToIntPtr(buffer4, 0);
    }

    public void ReadBeatmap()
    {
        EnsureBuffer(ref buffer, 320);
        SafeReadProcessMemory(process.Handle, pBeatmap, buffer, 320, ref bytesRead);
        SliderMultiplier = BitConverter.ToDouble(buffer, 8);
        SliderTickRate = BitConverter.ToDouble(buffer, 16);
        ApproachRate = BitConverter.ToSingle(buffer, 44);
        CircleSize = BitConverter.ToSingle(buffer, 48);
        HPDrainRate = BitConverter.ToSingle(buffer, 52);
        OverallDifficulty = BitConverter.ToSingle(buffer, 56);
        ContainingFolder = ReadString(ToIntPtr(buffer, 120));
        Filename = ReadString(ToIntPtr(buffer, 144));
        BeatmapVersion = BitConverter.ToInt32(buffer, 216);
        PreviewTime = BitConverter.ToInt32(buffer, 288);
        StackLeniency = BitConverter.ToSingle(buffer, 296);
        TimelineZoom = BitConverter.ToSingle(buffer, 304);
    }

    public void SetControlPoints()
    {
        EnsureBuffer(ref buffer, 192);
        SafeReadProcessMemory(process.Handle, pBeatmap, buffer, 192, ref bytesRead);
        pControlPointsL = ToIntPtr(buffer, 176);
        SafeReadProcessMemory(process.Handle, pControlPointsL, buffer16, 16, ref bytesRead);
        pControlPointsA = ToIntPtr(buffer16, 4);
        numControlPoints = SafeBitConverterToInt32(buffer16, 12, "numControlPoints");
        EnsureBuffer(ref pControlPoints, 4 * numControlPoints);
        SafeReadProcessMemory(process.Handle, pControlPointsA + 8, pControlPoints, 4 * numControlPoints, ref bytesRead);
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
        SafeReadProcessMemory(process.Handle, pControlPoint, bufferCp, 48, ref bytesRead);
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
        SafeReadProcessMemory(process.Handle, pObjectsL, buffer16, 16, ref bytesRead);
        pObjectsA = ToIntPtr(buffer16, 4);
        numObjects = SafeBitConverterToInt32(buffer16, 12, "numObjects");
        EnsureBuffer(ref pObjects, 4 * numObjects);
        SafeReadProcessMemory(process.Handle, pObjectsA + 8, pObjects, 4 * numObjects, ref bytesRead);

        // 建立指针 -> 主物件下标映射，供高频选中读取使用
        if (masterIndexByPointer == null || masterIndexByPointer.Count != numObjects)
        {
            masterIndexByPointer = new Dictionary<IntPtr, int>(numObjects);
        }
        else
        {
            masterIndexByPointer.Clear();
        }
        for (int i = 0; i < numObjects; i++)
        {
            masterIndexByPointer[ToIntPtr(pObjects, 4 * i)] = i;
        }
    }

    public void ReadObjects(bool fetchHitSound = true)
    {
        hitObjects = new List<HitObject>();
        for (int i = 0; i < numObjects; i++)
        {
            hitObjects.Add(ReadObject(ToIntPtr(pObjects, 4 * i), fetchHitSound));
        }
    }

    private HitObject ReadObject(IntPtr pObject, bool fetchHitSound)
    {
        SafeReadProcessMemory(process.Handle, pObject, bufferOb, 336, ref bytesRead);
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
        if (hitObject.IsSlider())
        {
            hitObject.curveLength = BitConverter.ToDouble(bufferOb, 148);
            hitObject.CurveType = BitConverter.ToInt32(bufferOb, 248);
            hitObject.unifiedSoundAddition = (fetchHitSound) ? BitConverter.ToBoolean(bufferOb, 286) : true;
            pPointsL = ToIntPtr(bufferOb, 196);
            pSTL = ToIntPtr(bufferOb, 224);
            pSSL = ToIntPtr(bufferOb, 228);
            pSSAL = ToIntPtr(bufferOb, 232);
            SafeReadProcessMemory(process.Handle, pPointsL, buffer16, 16, ref bytesRead);
            pTempA = ToIntPtr(buffer16, 4);
            numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
            EnsureBuffer(ref bTemp, 8 * numTemp);
            SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 8 * numTemp, ref bytesRead);
            hitObject.sliderCurvePoints = new float[2 * numTemp];
            Buffer.BlockCopy(bTemp, 0, hitObject.sliderCurvePoints, 0, 8 * numTemp);
            if (!hitObject.unifiedSoundAddition)
            {
                SafeReadProcessMemory(process.Handle, pSTL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SoundTypeList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SoundTypeList, 0, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pSSL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
                hitObject.SampleSetList = new int[numTemp];
                Buffer.BlockCopy(bTemp, 0, hitObject.SampleSetList, 0, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pSSAL, buffer16, 16, ref bytesRead);
                pTempA = ToIntPtr(buffer16, 4);
                numTemp = SafeBitConverterToInt32(buffer16, 12, "numTemp");
                EnsureBuffer(ref bTemp, 4 * numTemp);
                SafeReadProcessMemory(process.Handle, pTempA + 8, bTemp, 4 * numTemp, ref bytesRead);
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

    public void FetchObjects(bool fetchHitSound = true)
    {
        SetObjects();
        ReadObjects(fetchHitSound);
    }

    public void FetchAll(bool fetchFull = true)
    {
        FetchHOM();
        FetchBeatmap();
        FetchControlPoints();
        FetchObjects(fetchFull);
        if (fetchFull) FetchBookmarks();
    }
}
