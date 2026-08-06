using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace osucatch_editor_realtimeviewer
{
    public class PeriodicTaskRunner
    {
        private readonly Func<CancellationToken, Task> _task;
        private CancellationTokenSource _cts;
        private Task _runTask;
        private long _lastStartTimestamp;
        private long _intervalTicks;
        private long _errorDelayTicks;

        public PeriodicTaskRunner(double intervalTime, double errorDelayTime, Func<CancellationToken, Task> task)
        {
            _task = task;
            SetInterval(intervalTime, errorDelayTime);
        }

        public void SetInterval(double intervalTime, double errorDelayTime)
        {
            _intervalTicks = (long)(Stopwatch.Frequency * intervalTime / 1000);
            _errorDelayTicks = (long)(Stopwatch.Frequency * errorDelayTime / 1000);
        }

        public void Start()
        {
            // 提高系统时钟分辨率到 1ms，让 Task.Delay 在 10ms 级间隔下减少抖动
            timeBeginPeriod(1);
            _cts = new CancellationTokenSource();
            _runTask = RunLoopAsync(_cts.Token);
        }

        public async Task StopAsync()
        {
            if (_cts == null) return;

            _cts.Cancel();
            try
            {
                await _runTask;
            }
            catch (OperationCanceledException) { }
            finally
            {
                _cts.Dispose();
                _cts = null;
                timeEndPeriod(1);
            }
        }

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        private async Task RunLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                long now = Stopwatch.GetTimestamp();
                long elapsedTicks = now - _lastStartTimestamp;
                long waitTicks = _intervalTicks - elapsedTicks;

                // 等待到下一个执行时间点
                if (waitTicks > 0 && _lastStartTimestamp != 0)
                {
                    double waitMs = (double)waitTicks / Stopwatch.Frequency * 1000;
                    await Task.Delay((int)waitMs, ct).ConfigureAwait(false);
                }

                _lastStartTimestamp = Stopwatch.GetTimestamp();
                bool taskFailed = false;

                try
                {
                    // 执行任务并等待完成
                    await _task(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
                {
                    Log.ConsoleLog(ex.ToString(), Log.LogType.Program, Log.LogLevel.Error);
                    return; // 整体任务被取消
                }
                catch (Exception ex)
                {
                    // Log.ConsoleLog(ex.ToString(), Log.LogType.Program, Log.LogLevel.Error);
                    taskFailed = true;
                }

                // 处理任务失败的情况
                if (taskFailed)
                {
                    try
                    {
                        // 长延迟
                        double waitMs = (double)_errorDelayTicks / Stopwatch.Frequency * 1000;
                        await Task.Delay((int)waitMs, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        return;
                    }
                }
            }
        }
    }
}
