using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace FUTO.MDNS;

public static class Utilities
{
    public static List<IPAddress> GetIPs(IEnumerable<NetworkInterface> networkInterfaces)
    {
        return networkInterfaces.SelectMany(v => v.GetIPProperties()
            .UnicastAddresses
            .Select(x => x.Address)
            .Where(x => !IPAddress.IsLoopback(x) && x.AddressFamily == AddressFamily.InterNetwork))
            .ToList();
    }

    public static List<IPAddress> GetIPs()
    {
        return GetIPs(NetworkInterface.GetAllNetworkInterfaces());
    }
}