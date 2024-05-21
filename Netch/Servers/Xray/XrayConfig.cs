#nullable disable
// ReSharper disable InconsistentNaming

using System.Text.Json.Serialization;

namespace Netch.Servers;

public struct XrayConfig
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object log { get; set; }

    public object[] inbounds { get; set; }

    public object outbounds { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object api { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object policy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public object stats { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public X_Route routing { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public X_DNS dns { get; set; }
}

public class X_DNS()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> servers { get; set; }
}

public class X_Route ()
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string domainStrategy { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<object> rules { get; set; }
}