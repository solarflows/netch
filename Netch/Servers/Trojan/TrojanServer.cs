using Netch.Models;

namespace Netch.Servers;

public class TrojanServer : Server
{
    private string _tlsSecureType = VLESSGlobal.TLSSecure[1];

    public override string Type { get; } = "Trojan";

    public override string MaskedData()
    {
        return "";
    }

    /// <summary>
    ///     密码
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    ///     伪装域名
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    ///     传输模式 (如 tcp, ws, grpc)
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    ///     传输协议 (统一映射至 Mode)
    /// </summary>
    public string TransferProtocol
    {
        get => !string.IsNullOrEmpty(Mode) ? Mode : "tcp";
        set => Mode = value;
    }

    /// <summary>
    ///     WebSocket 请求路径
    /// </summary>
    public string? Path { get; set; } = "/";

    /// <summary>
    ///     GRPC Service Name
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    ///     TLS 底层传输安全
    /// </summary>
    public string TLSSecureType
    {
        get => _tlsSecureType;
        set
        {
            if (value == "")
                value = VLESSGlobal.TLSSecure[1];

            _tlsSecureType = value;
        }
    }
}