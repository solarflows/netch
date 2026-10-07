using System.Collections.Concurrent;
using Microsoft.VisualStudio.Threading;
using Netch.Models;
using Timer = System.Timers.Timer;

namespace Netch.Utils;

public static class DelayTestHelper
{
    private static readonly Timer Timer;

    private static readonly AsyncSemaphore PoolLock = new(16);

    public static readonly NumberRange Range = new(0, int.MaxValue / 1000);

    private static bool _enabled = true;

    private static int _isRunning = 0;

    /// <summary>
    ///     正在执行测速的单节点取消令牌字典（用于单节点精准熔断）
    /// </summary>
    private static readonly ConcurrentDictionary<Server, CancellationTokenSource> ActiveNodeCtsMap = new();

    /// <summary>
    ///     单节点测速完成事件（供 UI 局部更新，避免全量刷新卡顿）
    /// </summary>
    public static event Action<Server>? ServerTested;

    /// <summary>
    ///     测速状态变更事件 (true: 正在测试, false: 空闲/完成)
    /// </summary>
    public static event Action<bool>? TestingStateChanged;

    /// <summary>
    ///     当前是否正在测速
    /// </summary>
    public static bool IsTesting => _isRunning == 1;

    static DelayTestHelper()
    {
        // 采用单次触发模式，测速完成并冷却完毕后再重新排程，彻底杜绝队列无限堆积
        Timer = new Timer
        {
            AutoReset = false
        };

        Timer.Elapsed += (_, _) => AutoTestCycleAsync().Forget();
    }

    public static bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            UpdateTick();
        }
    }

    /// <summary>
    ///     用户手动触发单节点测速（高优先级，若该节点恰在后台测试中，精准打断旧任务）
    /// </summary>
    public static async Task TestServerAsync(Server server, CancellationToken ct = default)
    {
        // 精准打断：如果该节点当前恰好在后台自动队列中排队或正在探测，仅取消该节点的后台任务
        if (ActiveNodeCtsMap.TryRemove(server, out var existingCts))
        {
            try
            {
                existingCts.Cancel();
                existingCts.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ActiveNodeCtsMap[server] = linkedCts;

        try
        {
            await server.PingAsync(linkedCts.Token);
            ServerTested?.Invoke(server);
        }
        catch (OperationCanceledException)
        {
            // 用户取消
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Manual ping failed for {Remark}", server.Remark);
        }
        finally
        {
            ActiveNodeCtsMap.TryRemove(server, out _);
        }
    }

    /// <summary>
    ///     手动触发全量测试（等待完成）
    /// </summary>
    public static async Task PerformTestAsync(bool waitFinish = false)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            // 若已有测试在运行中，直接跳过防重入
            return;
        }

        TestingStateChanged?.Invoke(true);
        try
        {
            await ExecuteTestRoundsAsync(CancellationToken.None);
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
            TestingStateChanged?.Invoke(false);
            ScheduleNextCooldown();
        }
    }

    /// <summary>
    ///     后台自动测速轮次
    /// </summary>
    private static async Task AutoTestCycleAsync()
    {
        if (!Enabled)
            return;

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            // 防重入：上一轮还在运行，坚决丢弃本次触发，绝不入队等待
            return;
        }

        TestingStateChanged?.Invoke(true);
        try
        {
            await ExecuteTestRoundsAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Auto test cycle encountered error");
        }
        finally
        {
            Interlocked.Exchange(ref _isRunning, 0);
            TestingStateChanged?.Invoke(false);
            ScheduleNextCooldown();
        }
    }

    /// <summary>
    ///     执行两轮全量采样 + 第三轮差量边缘复测
    /// </summary>
    private static async Task ExecuteTestRoundsAsync(CancellationToken ct)
    {
        var allServers = Global.Settings.Server.ToList();
        if (allServers.Count == 0)
            return;

        const int pageSize = 16;
        Log.Debug("Starting multi-round latency test for {Count} servers...", allServers.Count);

        // ===== 轮次 1：全量快速切片采样 =====
        await RunBatchAsync(allServers, pageSize, ct);

        await Task.Delay(200, ct);

        // ===== 轮次 2：全量二次切片采样（平滑抖动，取最优值） =====
        var round1Snapshot = allServers.ToDictionary(s => s, s => s.Delay);
        await RunBatchAsync(allServers, pageSize, ct, onTested: s =>
        {
            // 节点作为状态表：若第二轮得到更优延迟或第一轮失败但第二轮成功，采纳更优值
            if (round1Snapshot.TryGetValue(s, out var r1Delay) && r1Delay >= 0)
            {
                if (s.Delay >= 0 && s.Delay > r1Delay)
                {
                    // 第一轮更优，回滚保留更优值
                    s.SetDelay(r1Delay);
                }
            }
        });

        await Task.Delay(200, ct);

        // ===== 轮次 3：差量复测（仅挑选前两轮中无响应/超时的失败节点进行最后一次确认） =====
        var failedServers = allServers.Where(s => s.Delay < 0).ToList();
        if (failedServers.Count > 0)
        {
            Log.Debug("Round 3 differential test: retrying {FailedCount} edge/timeout servers...", failedServers.Count);
            await RunBatchAsync(failedServers, pageSize, ct);
        }

        Log.Debug("Multi-round latency test completed for {Count} servers.", allServers.Count);
    }

    private static async Task RunBatchAsync(List<Server> servers, int pageSize, CancellationToken ct, Action<Server>? onTested = null)
    {
        for (int i = 0; i < servers.Count; i += pageSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = servers.Skip(i).Take(pageSize).ToList();

            var tasks = batch.Select(async s =>
            {
                using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                ActiveNodeCtsMap[s] = nodeCts;

                try
                {
                    using (await PoolLock.EnterAsync())
                    {
                        await s.PingAsync(nodeCts.Token);
                    }

                    onTested?.Invoke(s);
                    ServerTested?.Invoke(s);
                }
                catch (OperationCanceledException)
                {
                    // 该节点被用户手动测试精准打断或全局取消
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Ping failed in batch for {Remark}", s.Remark);
                }
                finally
                {
                    ActiveNodeCtsMap.TryRemove(s, out _);
                }
            });

            await Task.WhenAll(tasks);

            // 批次间歇微冷却 150ms，释放套接字与线程压力
            if (i + pageSize < servers.Count)
                await Task.Delay(150, ct);
        }
    }

    /// <summary>
    ///     完成一整轮测试后，强制启动确定性冷却定时器（至少 60 秒）
    /// </summary>
    private static void ScheduleNextCooldown()
    {
        Timer.Stop();
        if (!Enabled)
            return;

        // 冷却时间以当前轮次完全结束时起算，默认至少 60 秒，完全杜绝堆积
        var cooldownSeconds = Math.Max(60, Global.Settings.DetectionTick);
        Timer.Interval = cooldownSeconds * 1000;
        Timer.Start();
    }

    public static void UpdateTick(bool performTestAtOnce = false)
    {
        UpdateTick(Global.Settings.DetectionTick, performTestAtOnce);
    }

    private static void UpdateTick(int interval, bool performTestAtOnce = false)
    {
        Timer.Stop();

        var enable = Enabled && interval > 0 && Range.InRange(interval);
        if (enable)
        {
            var cooldownSeconds = Math.Max(60, interval);
            Timer.Interval = cooldownSeconds * 1000;
            Timer.Start();

            if (performTestAtOnce)
                AutoTestCycleAsync().Forget();
        }
    }
}
