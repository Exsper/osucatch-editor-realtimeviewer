using System.Threading;

namespace osucatch_editor_realtimeviewer
{
    /// <summary>
    /// 心跳 + UI 卡死检测：
    /// 后台每 10 秒写一条“存活”日志（即使 UI 线程卡死也能写）；
    /// 通过定时向 UI 线程投递脉冲探测其是否响应，超过阈值记录卡死信息。
    /// 用于定位“窗口未响应后闪退”这类没有托管异常的崩溃。
    /// </summary>
    public sealed class HealthMonitor : IDisposable
    {
        private const int PulseIntervalMs = 5000;
        private const int HeartbeatIntervalMs = 10000;
        private const int UnresponsiveThresholdMs = 15000;

        private readonly Control uiControl;
        private readonly System.Threading.Timer monitorTimer;
        private long lastUiPulseTicks;
        private long lastHeartbeatTicks;
        private bool uiUnresponsiveReported;
        private bool disposed;

        public HealthMonitor(Control uiControl)
        {
            this.uiControl = uiControl;
            long now = DateTime.UtcNow.Ticks;
            lastUiPulseTicks = now;
            lastHeartbeatTicks = now;
            monitorTimer = new System.Threading.Timer(OnTick, null, 0, PulseIntervalMs);
        }

        private void OnTick(object? state)
        {
            if (disposed) return;

            long now = DateTime.UtcNow.Ticks;
            long uiPulseAgeMs = (now - Interlocked.Read(ref lastUiPulseTicks)) / TimeSpan.TicksPerMillisecond;

            // 每 10 秒写一条存活日志；UI 卡死时这条仍会写入（写日志的是后台线程）
            if (now - lastHeartbeatTicks >= HeartbeatIntervalMs * TimeSpan.TicksPerMillisecond)
            {
                lastHeartbeatTicks = now;
                long memoryMb = GC.GetTotalMemory(false) / 1024 / 1024;
                Log.Breadcrumb("Heartbeat: alive, managed memory " + memoryMb + " MB, UI pulse " + uiPulseAgeMs + " ms ago.");
            }

            // 只在状态切换时记录卡死/恢复，避免刷屏
            bool unresponsive = uiPulseAgeMs > UnresponsiveThresholdMs;
            if (unresponsive && !uiUnresponsiveReported)
            {
                uiUnresponsiveReported = true;
                Log.Breadcrumb("UI thread unresponsive for " + uiPulseAgeMs + " ms.");
            }
            else if (!unresponsive && uiUnresponsiveReported)
            {
                uiUnresponsiveReported = false;
                Log.Breadcrumb("UI thread responsive again.");
            }

            // 在 UI 线程上打点：UI 卡死时该调用排队不执行，脉冲年龄持续增长
            try
            {
                if (!uiControl.IsDisposed)
                {
                    uiControl.BeginInvoke(new Action(() =>
                    {
                        if (!disposed) Interlocked.Exchange(ref lastUiPulseTicks, DateTime.UtcNow.Ticks);
                    }));
                }
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            disposed = true;
            monitorTimer.Dispose();
        }
    }
}
