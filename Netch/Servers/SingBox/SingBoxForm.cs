using Netch.Forms;

namespace Netch.Servers;

[Fody.ConfigureAwait(true)]
public class SingBoxForm : ServerForm
{
    public SingBoxForm(SingBoxServer? server = default)
    {
        server ??= new SingBoxServer();
        Server = server;

        CreateTextBox("Password", "Password", s => true, s => server.Password = s, server.Password);

        CreateComboBox("OBFS", "OBFS", SingBoxGlobal.OBFSs, s => server.OBFS = s, server.OBFS);

        CreateTextBox("OBFSParam", "OBFS Param", s => true, s => server.OBFSParam = s, server.OBFSParam);

        CreateTextBox("Sni", "ServerName(Sni)", s => true, s => server.ServerName = s, server.ServerName);
    }

    protected override string TypeName { get; } = "SingBox";
}