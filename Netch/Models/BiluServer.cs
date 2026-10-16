namespace Netch.Models;

/// <summary>
/// Contains name-to-URL mappings for Bilu bypass IP list sources.
/// </summary>
public class BiluServer
{
    public static readonly Dictionary<string, string> NamesAndUrls = new()
    {
        { "Clang.CN", "https://ispip.clang.cn/all_cn.txt" },
        { "Clang.CN.CIDR", "https://ispip.clang.cn/all_cn_cidr.txt" },
        { "china-operator-ip", "https://fastly.jsdelivr.net/gh/gaoyifan/china-operator-ip@ip-lists/china.txt" },
    };
}
