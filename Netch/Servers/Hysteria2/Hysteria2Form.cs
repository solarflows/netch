using Netch.Forms;

namespace Netch.Servers;

[Fody.ConfigureAwait(true)]
public class Hysteria2Form : ServerForm
{
    public Hysteria2Form(Hysteria2Server? server = default)
    {
        server ??= new Hysteria2Server();
        Server = server;

        CreateTextBox("Password", "Password", s => true, s => server.Password = s, server.Password);

        CreateComboBox("OBFS", "OBFS", HysteriaGlobal.OBFSs, s => server.OBFS = s, server.OBFS);

        CreateTextBox("OBFSParam", "OBFS Param", s => true, s => server.OBFSParam = s, server.OBFSParam);

        CreateTextBox("Sni", "ServerName(Sni)", s => true, s => server.ServerName = s, server.ServerName);
    }

    protected override string TypeName { get; } = "Hysteria2";
}