// SyncClient, Version=1.6.0.0, Culture=neutral, PublicKeyToken=null
// SyncClient.OSHelper

using System.Diagnostics;
using System.Runtime.InteropServices;
using SyncShared;

namespace SyncClient;

public class OSHelper
{
    public static string GetComputerName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Environment.MachineName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ExecuteCommand("scutil --get ComputerName").Trim();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string machineName;
            try
            {
                machineName = Environment.MachineName;
                if (!string.IsNullOrEmpty(machineName)) return machineName;
            }
            catch (Exception ex)
            {
                Logger.Error<OSHelper>("Error fetching hostname, trying different method...", ex);
            }

            try
            {
                machineName = ExecuteCommand("hostnamectl hostname").Trim();
                if (!string.IsNullOrEmpty(machineName)) return machineName;
            }
            catch (Exception ex2)
            {
                Logger.Error<OSHelper>("Error fetching hostname again, using generic name...", ex2);
                machineName = "linux device";
            }

            return machineName;
        }

        return Environment.MachineName;
    }

    private static string ExecuteCommand(string command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = "-c \"" + command + "\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new Process
        {
            StartInfo = startInfo
        };
        process.Start();
        var result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}