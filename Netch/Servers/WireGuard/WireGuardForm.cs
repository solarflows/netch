using Netch.Forms;

namespace Netch.Servers;

[Fody.ConfigureAwait(true)]
public class WireGuardForm : ServerForm
{
    public WireGuardForm(WireGuardServer? server = default)
    {
        server ??= new WireGuardServer();
        Server = server;
        CreateTextBox("PeerPublicKey",  "Public Key",       s => true,                      s => server.PeerPublicKey = s,      server.PeerPublicKey);
        CreateTextBox("LocalAddresses", "Local Addresses",  s => true,                      s => server.LocalAddresses = s,     server.LocalAddresses);
        CreateTextBox("PrivateKey",     "Private Key",      s => true,                      s => server.PrivateKey = s,         server.PrivateKey);
        CreateTextBox("PreSharedKey",   "PSK",              s => true,                      s => server.PreSharedKey = s,       server.PreSharedKey);
        CreateTextBox("MTU",            "MTU",              s => int.TryParse(s, out _),    s => server.MTU = int.Parse(s),     server.MTU.ToString(), 76);
        CreateTextBox("Reserved",       "Reserved",         s => CheckReserved(s),          s => server.Reserved = s,           server.Reserved, 76);
        CreateTextBox("Workers",        "Workers",          s => int.TryParse(s, out _),    s => server.Workers = int.Parse(s), server.Workers.ToString(), 76);
        CreateTextBox("KeepAlive",      "Keep Alive",       s => int.TryParse(s, out _),    s => server.KeepAlive = int.Parse(s),server.KeepAlive.ToString(), 76);
        CreateTextBox("AllowIPs",       "Allow Ips",        s => true,                      s => server.AllowIPs = s,           server.AllowIPs);
        CreateComboBox("DomainStrategy", "Domain Strategy", server.GetDomainStrategyList(), s => server.DomainStrategy=s,       server.DomainStrategy);
    }

    protected override string TypeName { get; } = "WireGuard";

    private bool CheckReserved(string s)
    {
        var s_reserved = s.Split(",");
        var int_reserved = s_reserved.Select(x => int.TryParse(x, out _)).ToList();
        return int_reserved.Count == 3;
    }
}