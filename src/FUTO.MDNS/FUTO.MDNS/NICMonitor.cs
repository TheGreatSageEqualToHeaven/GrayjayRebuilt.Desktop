using System.Net.NetworkInformation;

namespace FUTO.MDNS
{
    public class NICMonitor
    {
        private readonly object _lockObject = new object();
        private readonly List<NetworkInterface> _nics = new List<NetworkInterface>();
        private CancellationTokenSource? _cts;

        public List<NetworkInterface> Current
        {
            get
            {
                lock (_nics)
                {
                    return _nics.ToList();
                }
            }
        }
        public event Action<List<NetworkInterface>>? Added;
        public event Action<List<NetworkInterface>>? Removed;

        public void Start()
        {
            lock (_lockObject)
            {
                if (_cts != null)
                    throw new Exception("Already started.");

                _cts = new CancellationTokenSource();
            }

            _nics.Clear();
            _nics.AddRange(GetCurrent().ToList());
            
            _ = LoopAsync(_cts.Token);
        }

        public void Stop()
        {
            lock (_lockObject)
            {
                _cts?.Cancel();
                _cts = null;
            }

            lock (_nics)
            {
                _nics.Clear();
            }
        }

        private async Task LoopAsync (CancellationToken cancellationToken = default)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var currentNics = GetCurrent().ToList();
                    Removed?.Invoke(_nics.Where(k => !currentNics.Any(n => k.Id == n.Id)).ToList());
                    Added?.Invoke(currentNics.Where(nic => !_nics.Any(k => k.Id == nic.Id)).ToList());

                    lock (_nics)
                    {
                        _nics.Clear();
                        _nics.AddRange(currentNics);
                    }
                }
                catch
                {
                    //Ignored
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        private IEnumerable<NetworkInterface> GetCurrent()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();
                
            if (nics.Length > 0)
                return nics;

            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up);
        }
    }
}