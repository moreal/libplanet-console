using System.Diagnostics;
using System.Net;
using Libplanet.Crypto;
using LibplanetConsole.Common;
using static LibplanetConsole.Consoles.ProcessUtility;

namespace LibplanetConsole.Consoles;

internal sealed class NodeProcess : IDisposable
{
    private readonly Process _process;

    public NodeProcess(EndPoint endPoint, PrivateKey privateKey, string storeDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            ArgumentList =
            {
                "--end-point",
                EndPointUtility.ToString(endPoint),
                "--private-key",
                PrivateKeyUtility.ToString(privateKey),
                "--parent",
                Environment.ProcessId.ToString(),
            },
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            FileName = IsWindows() ? NodePath : "dotnet",
        };
        if (IsWindows() != true)
        {
            startInfo.ArgumentList.Insert(0, NodePath);
        }

        if (storeDirectory != string.Empty)
        {
            startInfo.ArgumentList.Add("--store-path");
            startInfo.ArgumentList.Add(
                Path.Combine(storeDirectory, (ShortAddress)privateKey.Address));
        }
        else
        {
            startInfo.ArgumentList.Add("--volatile");
        }

        _process = new Process
        {
            StartInfo = startInfo,
        };
        _process.Start();
    }

    public event EventHandler? Exited
    {
        add => _process.Exited += value;
        remove => _process.Exited -= value;
    }

    public int Id => _process.Id;

    public void Dispose()
    {
        if (_process.HasExited != true)
        {
            _process.Close();
        }
    }
}
