using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Shapes;
using MaxMind.GeoIP2.Model;
using Netch.Models;
using Netch.Models.GitHubRelease;
using Netch.Utils;
using Newtonsoft.Json;
using Path = System.IO.Path;
using System.Net.Http;

namespace Netch.Controllers;

public static class UpdateChecker
{
    public const string Owner = @"domparso";
    public const string Repo = @"netch";

    public const string Name = @"Netch";
    public const string Copyright = @"Copyright © 2019 - 2024";

    public const string AssemblyVersion = @"1.9.9";
    private const string Suffix = @"";

    public static readonly string Version = $"{AssemblyVersion}{(string.IsNullOrEmpty(Suffix) ? "" : $"-{Suffix}")}";

    public static Release LatestRelease = null!;

    public static string LatestVersionNumber => LatestRelease.tag_name;

    public static string LatestVersionUrl => LatestRelease.html_url;

    public static event EventHandler? NewVersionFound;

    public static event EventHandler? NewVersionFoundFailed;

    public static event EventHandler? NewVersionNotFound;

    public static async Task CheckAsync(bool isPreRelease)
    {
        try
        {
            var updater = new GitHubRelease(Owner, Repo);
            var url = updater.AllReleaseUrl;

            var (_, json) = await WebUtil.DownloadStringAsync(WebUtil.CreateRequest(url));

            var releases = System.Text.Json.JsonSerializer.Deserialize<List<Release>>(json)!;
            LatestRelease = GetLatestRelease(releases, isPreRelease);
            Log.Information("Github latest release: {Version}", LatestRelease.tag_name);
            if (VersionUtil.CompareVersion(LatestRelease.tag_name, Version) > 0)
            {
                Log.Information("Found newer version");
                NewVersionFound?.Invoke(null, EventArgs.Empty);
            }
            else
            {
                Log.Information("Already the latest version");
                NewVersionNotFound?.Invoke(null, EventArgs.Empty);
            }
        }
        catch (Exception e)
        {
            if (e is WebException)
                Log.Warning(e, "Get releases failed");
            else
                Log.Error(e, "Get releases error");

            NewVersionFoundFailed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static (string fileName, string sha256) GetLatestUpdateFileNameAndHash(string? keyword = null)
    {
        var matches = Regex.Matches(LatestRelease.body, @"^\| (?<filename>.*) \| (?<sha256>.*) \|\r?$", RegexOptions.Multiline).Skip(2);
        /*
          Skip(2)
          
          | 文件名 | SHA256 |
          | :- | :- |
       */

        Match match = keyword == null ? matches.First() : matches.First(m => m.Groups["filename"].Value.Contains(keyword));

        return (match.Groups["filename"].Value, match.Groups["sha256"].Value);
    }

    public static string GetLatestReleaseContent()
    {
        var sb = new StringBuilder();
        foreach (string l in LatestRelease.body.GetLines(false).SkipWhile(l => l.FirstOrDefault() != '#'))
        {
            if (l.Contains("校验和"))
                break;

            sb.AppendLine(l);
        }

        return sb.ToString();
    }

    private static Release GetLatestRelease(IEnumerable<Release> releases, bool isPreRelease)
    {
        if (!isPreRelease)
            releases = releases.Where(release => !release.prerelease);

        var ordered = releases.OrderByDescending(release => release.tag_name, new VersionUtil.VersionComparer());
        return ordered.ElementAt(0);
    }
}


public static class CoreUpdate
{
    public const string GithubApi = @"https://api.github.com/repos/{0}/{1}/releases/latest";

    public const string V2rayRepo = @"https://github.com/v2fly/v2ray-core";
    public const string XrayRepo = @"https://github.com/XTLS/Xray-core";
    public const string SingBoxRepo = @"https://github.com/SagerNet/sing-box";
    public const string HysteriaRepo = @"https://github.com/apernet/hysteria";
    public const string TrojanRepo = @"";

    public static string RunCmd(string cmd)
    {
       
        System.Diagnostics.Process p = new System.Diagnostics.Process();
        p.StartInfo.FileName = "cmd.exe";
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;
        p.Start();

        p.StandardInput.WriteLine(cmd);
        p.StandardInput.AutoFlush = true;
        p.StandardInput.WriteLine("exit");

        string output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        p.Close();

        return output;
    }

    public static string Request(string repo)
    {
        string[] strs = repo.Split("/");
        if (strs.Length <= 0) { return "";}

        var Url = string.Format(GithubApi, strs[strs.Length - 2], strs[strs.Length - 1]);

        HttpWebRequest request = WebRequest.Create(Url) as HttpWebRequest;
        request.Method = "GET";
        request.UserAgent = "Mozilla/5.0";
        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        {
            Stream stream = response.GetResponseStream();
            string html = string.Empty;
            using (StreamReader reader = new StreamReader(stream))
            {
                html = reader.ReadToEnd();
                // Console.WriteLine(html);

                dynamic jsonObject = JsonConvert.DeserializeObject(html);
                return jsonObject["tag_name"];
            }
        }

        return "";
    }

    public static void DownloadFile(string fileFullPath, string url)
    {
        var fileStream = new FileStream(fileFullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        {
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0";

            using (fileStream)
            {
                using var webResponse = (HttpWebResponse) request.GetResponse();
                var input = webResponse.GetResponseStream();
                using (input)
                {
                    using var downloadTask = input.CopyToAsync(fileStream);

                    while (!downloadTask.IsCompleted)
                    {
                        Thread.Sleep(1000);
                    }

                }
            }

            
        }

    }

    public static bool CompareVersion(string oldVer, string newVer)
    {
        if (oldVer == newVer) return false;
        string[] olds;
        string[] news;

        if (oldVer.Contains("-beta."))
        {
            olds = oldVer.Split("-beta.");
            news = newVer.Split("-beta.");

            if (olds[0] == news[0])
            {

            }
        }

        return true;
    }

    public static string getNewVersion(string core)
    {
        var version = "";
        switch (core)
        {
            case "v2ray":
                version = Request(V2rayRepo);
                if (version.Length <= 0) { return ""; }

                break;
            case "xray":
                version = Request(XrayRepo);
                if (version.Length <= 0) { return ""; }

                break;
            case "hysteria":
                version = Request(HysteriaRepo);
                if (version.Length <= 0) { return ""; }

                break;
            case "singbox":
                version = Request(SingBoxRepo);
                if (version.Length <= 0) { return ""; }

                break;
            case "trojan":
                version = Request(TrojanRepo);
                if (version.Length <= 0) { return ""; }

                break;
            default:
                return "";
                break;
        }

        return version;
    }

    public static bool CheckUpgrade(string core)
    {
        string downloadDirectory = Path.Combine(Global.NetchDir, "data");
        var result = "";
        string pattern;
        string oldVersion = "";
        string newVersion = "";
        Regex regex;
        Match match;

        switch (core)
        {
            case "v2ray":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.V2rayBin)))
                {
                    result = RunCmd("v2ray version");
                    Console.WriteLine(result);
                    pattern = @"V2Ray (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return false;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return false;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/v2ray-windows-64.zip", V2rayRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "v2ray-windows-64.zip");
                    DownloadFile(updateFileFullName, url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    /*if (File.Exists(tmpPath))
                    {
                        File.Delete(tmpPath);
                    }*/
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, "v2ray.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.V2rayBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return true;
                }

                    break;
            case "xray":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.XrayBin)))
                {
                    result = RunCmd("xray version");
                    pattern = @"Xray (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return false;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return false;
                    }
                }
                
                if(newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/Xray-windows-64.zip", XrayRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "Xray-windows-64.zip");
                    DownloadFile(updateFileFullName, url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, "xray.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.XrayBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return true;
                }

                break;
            case "hysteria":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.HysteriaBin)))
                {
                    result = RunCmd("hysteria version");
                    pattern = @"Version:.*v(\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return false;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return false;
                    }
                }
                
                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/hysteria-windows-amd64.exe", HysteriaRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "hysteria-windows-amd64.exe");
                    DownloadFile(updateFileFullName, url);

                    File.Move(Path.Combine(downloadDirectory, "hysteria-windows-amd64.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.HysteriaBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    return true;
                }

                break;
            case "singbox":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.SingBoxBin)))
                {
                    result = RunCmd("sing-box version");
                    pattern = @"version (\d+\.\d+\.\d+-\w+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return false;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return false;
                    }
                } 
                
                if (newVersion.Length > 0)
                {
                    string singboxFileName = string.Format("sing-box-{0}-windows-amd64.zip", newVersion.Substring(1));
                    string singboxFileName1 = string.Format("sing-box-{0}-windows-amd64", newVersion.Substring(1));
                    var url = string.Format("{0}/releases/download/{1}/{2}", SingBoxRepo, newVersion, singboxFileName);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, singboxFileName);
                    DownloadFile(updateFileFullName, url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, singboxFileName1, "sing-box.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.SingBoxBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return true;
                }

                break;
            case "trojan":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.TrojanBin)))
                {
                    result = RunCmd("trojan --version");
                    pattern = @"trojan (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return false;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return false;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/{}.zip", V2rayRepo, newVersion);
                    var updateFileFullName = Path.Combine(downloadDirectory, Global.Settings.Core.TrojanBin);
                    return false;
                }

                break;
            default :

                break;
        }

        return false;
    }

    public static async void CheckUpgradeAsync(string core)
    {
        string downloadDirectory = Path.Combine(Global.NetchDir, "data");
        var result = "";
        string pattern;
        string oldVersion = "";
        string newVersion = "";
        Regex regex;
        Match match;

        switch (core)
        {
            case "v2ray":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.V2rayBin)))
                {
                    result = RunCmd("v2ray version");
                    Console.WriteLine(result);
                    pattern = @"V2Ray (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return ;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return ;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/v2ray-windows-64.zip", V2rayRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "v2ray-windows-64.zip");
                    DownloadFile(updateFileFullName, url);

                    HttpClient client = new HttpClient();

                    // Download the zip file
                    byte[] zipBytes = await client.GetByteArrayAsync(url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    /*if (File.Exists(tmpPath))
                    {
                        File.Delete(tmpPath);
                    }*/
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, "v2ray.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.V2rayBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return ;
                }

                break;
            case "xray":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.XrayBin)))
                {
                    result = RunCmd("xray version");
                    pattern = @"Xray (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return ;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return ;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/Xray-windows-64.zip", XrayRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "Xray-windows-64.zip");
                    DownloadFile(updateFileFullName, url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, "xray.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.XrayBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return ;
                }

                break;
            case "hysteria":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.HysteriaBin)))
                {
                    result = RunCmd("hysteria version");
                    pattern = @"Version:.*v(\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return ;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return ;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/hysteria-windows-amd64.exe", HysteriaRepo, newVersion);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, "hysteria-windows-amd64.exe");
                    DownloadFile(updateFileFullName, url);

                    File.Move(Path.Combine(downloadDirectory, "hysteria-windows-amd64.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.HysteriaBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    return ;
                }

                break;
            case "singbox":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.SingBoxBin)))
                {
                    result = RunCmd("sing-box version");
                    pattern = @"version (\d+\.\d+\.\d+-\w+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return ;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return ;
                    }
                }

                if (newVersion.Length > 0)
                {
                    string singboxFileName = string.Format("sing-box-{0}-windows-amd64.zip", newVersion.Substring(1));
                    string singboxFileName1 = string.Format("sing-box-{0}-windows-amd64", newVersion.Substring(1));
                    var url = string.Format("{0}/releases/download/{1}/{2}", SingBoxRepo, newVersion, singboxFileName);
                    Console.WriteLine(url);
                    var updateFileFullName = Path.Combine(downloadDirectory, singboxFileName);
                    DownloadFile(updateFileFullName, url);

                    var tmpPath = Path.Combine(downloadDirectory, "tmp");
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    ZipFile.ExtractToDirectory(updateFileFullName, tmpPath);
                    File.Move(Path.Combine(tmpPath, singboxFileName1, "sing-box.exe"), Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.SingBoxBin), true);
                    if (File.Exists(updateFileFullName))
                    {
                        File.Delete(updateFileFullName);
                    }
                    if (Directory.Exists(tmpPath))
                    {
                        Directory.Delete(tmpPath, true);
                    }
                    return ;
                }

                break;
            case "trojan":
                newVersion = getNewVersion(core);
                if (File.Exists(Path.Combine(Global.NetchDir, "bin", Global.Settings.Core.TrojanBin)))
                {
                    result = RunCmd("trojan --version");
                    pattern = @"trojan (\d+\.\d+\.\d+)";
                    regex = new Regex(pattern);
                    match = regex.Match(result);
                    if (!match.Success)
                    {
                        return ;
                    }
                    oldVersion = match.Groups[1].Value;
                    Console.WriteLine(oldVersion);

                    if (oldVersion == newVersion)
                    {
                        return ;
                    }
                }

                if (newVersion.Length > 0)
                {
                    var url = string.Format("{0}/releases/download/{1}/{}.zip", V2rayRepo, newVersion);
                    var updateFileFullName = Path.Combine(downloadDirectory, Global.Settings.Core.TrojanBin);
                    return ;
                }

                break;
            default:

                break;
        }

        return ;
    }

    public static async Task CheckAsync()
    {

        if (V2rayRepo.Length > 0)
        {
            await CheckUpgrade("v2ray");
        }

        if (XrayRepo.Length > 0)
        {
            await CheckUpgrade("xray");
        }

        if (SingBoxRepo.Length > 0)
        {
            await CheckUpgrade("singbox");
        }

        if (HysteriaRepo.Length > 0)
        {
            await CheckUpgrade("hysteria");
        }

        if (TrojanRepo.Length > 0)
        {
            await CheckUpgrade("trojan");
        }

    }
}