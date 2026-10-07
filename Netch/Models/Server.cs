using System.Net.Sockets;
using System.Text.Json.Serialization;
using Netch.Utils;

namespace Netch.Models;

public abstract class Server : ICloneable
{
    /// <summary>
    ///     延迟
    /// </summary>
    [JsonIgnore]
    public int Delay { get; private set; } = -1;

    [JsonIgnore]
    private string? _cachedDisplayText;

    [JsonIgnore]
    private string _group = Constants.DefaultGroup;

    [JsonIgnore]
    private string _hostname = string.Empty;

    [JsonIgnore]
    private ushort _port;

    [JsonIgnore]
    private string _remark = "";

    /// <summary>
    ///     组
    /// </summary>
    public string Group
    {
        get => _group;
        set
        {
            _group = value;
            _cachedDisplayText = null;
        }
    }

    /// <summary>
    ///     地址
    /// </summary>
    public string Hostname
    {
        get => _hostname;
        set
        {
            _hostname = value;
            _cachedDisplayText = null;
        }
    }

    /// <summary>
    ///     端口
    /// </summary>
    public ushort Port
    {
        get => _port;
        set
        {
            _port = value;
            _cachedDisplayText = null;
        }
    }

    /// <summary>
    ///     倍率
    /// </summary>
    public double Rate { get; } = 1.0;

    /// <summary>
    ///     备注
    /// </summary>
    public string Remark
    {
        get => _remark;
        set
        {
            _remark = value;
            _cachedDisplayText = null;
        }
    }

    /// <summary>
    ///     跳过证书验证 (针对伪装 SNI 或自签证书)
    /// </summary>
    public bool? AllowInsecure { get; set; }

    /// <summary>
    ///     代理类型
    /// </summary>
    [JsonPropertyOrder(int.MinValue)]
    public abstract string Type { get; }

    public object Clone()
    {
        return MemberwiseClone();
    }

    /// <summary>
    ///     获取备注 (O(1) 高速缓存直读，彻底消除大量项滚动时的字符串格式化开销)
    /// </summary>
    /// <returns>备注</returns>
    public override string ToString()
    {
        if (_cachedDisplayText != null)
            return _cachedDisplayText;

        var remark = string.IsNullOrWhiteSpace(Remark) ? $"{Hostname}:{Port}" : Remark;

        var shortName = ServerHelper.GetUtilByTypeName(Type).ShortName;

        return _cachedDisplayText = $"[{shortName}][{Group}] {remark}";
    }

    public abstract string MaskedData();

    [JsonIgnore]
    public DateTime LastTestTime { get; set; } = DateTime.MinValue;

    /// <summary>
    ///     更新延迟并记入状态表
    /// </summary>
    public int SetDelay(int delay)
    {
        LastTestTime = DateTime.UtcNow;
        return Delay = delay;
    }

    /// <summary>
    ///     测试延迟
    /// </summary>
    /// <returns>延迟</returns>
    public async Task<int> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var destination = await DnsUtils.LookupAsync(Hostname);
            if (destination == null)
            {
                return SetDelay(-2);
            }

            int result;
            if (Global.Settings.ServerTCPing)
            {
                result = await Utils.Utils.TCPingAsync(destination, Port, 1000, ct);
            }
            else
            {
                result = await Utils.Utils.ICMPingAsync(destination);
            }

            return SetDelay(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return SetDelay(-4);
        }
    }
}

public static class ServerExtension
{
    public static async Task<string> AutoResolveHostnameAsync(this Server server, AddressFamily inet = AddressFamily.Unspecified)
    {
        // ! MainController cached
        return (await DnsUtils.LookupAsync(server.Hostname, inet))!.ToString();
    }

    public static bool IsInGroup(this Server server)
    {
        return server.Group is not Constants.DefaultGroup;
    }
}