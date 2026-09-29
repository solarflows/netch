using System.Reflection;
using Netch.Interfaces;

namespace Netch.Utils;

public static class ServerHelper
{
    static ServerHelper()
    {
        var serversUtilsTypes = Assembly.GetExecutingAssembly()
            .GetExportedTypes()
            .Where(type => type.GetInterfaces().Contains(typeof(IServerUtil)));

        ServerUtilDictionary = new Dictionary<string, IServerUtil>(StringComparer.OrdinalIgnoreCase);
        foreach (var util in serversUtilsTypes.Select(t => (IServerUtil)Activator.CreateInstance(t)!))
        {
            ServerUtilDictionary[util.TypeName] = util;
            if (util.TypeName.Equals("SOCKS", StringComparison.OrdinalIgnoreCase))
            {
                ServerUtilDictionary["Socks5"] = util;
            }
        }
    }

    public static Dictionary<string, IServerUtil> ServerUtilDictionary { get; }

    public static IServerUtil GetUtilByTypeName(string typeName)
    {
        return ServerUtilDictionary.GetValueOrDefault(typeName) ?? throw new NotSupportedException($"Specified server type {typeName} is not supported.");
    }

    public static IServerUtil? GetUtilByUriScheme(string scheme)
    {
        return ServerUtilDictionary.Values.Distinct().SingleOrDefault(i => i.UriScheme.Any(s => s.Equals(scheme, StringComparison.OrdinalIgnoreCase)));
    }

    public static Type GetTypeByTypeName(string typeName)
    {
        return GetUtilByTypeName(typeName).ServerType;
    }
}