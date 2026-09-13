using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Icons.SlowHarness
{
    /// <summary>
    /// A host that answers the socket and nothing else.
    ///
    /// The stock icons are the one thing this tool asks for over plain HTTP rather than through
    /// the SDK, so <see cref="SlowService"/> cannot put latency on them - and the failure worth
    /// testing is not a refused connection but an accepted one that never says anything, which
    /// is what a proxy sitting on the request looks like from in here. A refusal comes back in
    /// milliseconds and costs the load nothing; this is the one that costs it the timeout.
    ///
    /// Every connection is accepted and held open until the scenario is over, so every read
    /// against it runs out its own clock rather than failing fast.
    /// </summary>
    public class DeadHost : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly List<TcpClient> _held = new List<TcpClient>();
        private volatile bool _stopped;

        private DeadHost(TcpListener listener)
        {
            _listener = listener;
            Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        /// <summary>Where the org appears to live. What a scenario hands the tool.</summary>
        public string Url { get; private set; }

        /// <summary>
        /// Up and listening. The port is the machine's to choose, so a suite run cannot collide
        /// with whatever else is listening here.
        /// </summary>
        public static DeadHost Standing()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            var host = new DeadHost(listener);
            host.Accept();
            return host;
        }

        private void Accept()
        {
            _listener.BeginAcceptTcpClient(result =>
            {
                if (_stopped) return;

                try
                {
                    var client = _listener.EndAcceptTcpClient(result);
                    lock (_held) _held.Add(client);
                }
                catch (Exception)
                {
                    // The listener stopped between the callback being queued and getting here,
                    // which is the end of the scenario rather than anything to report.
                    return;
                }

                Accept();
            }, null);
        }

        public void Dispose()
        {
            _stopped = true;
            try { _listener.Stop(); } catch (Exception) { }

            lock (_held)
            {
                foreach (var client in _held)
                {
                    try { client.Close(); } catch (Exception) { }
                }

                _held.Clear();
            }
        }
    }
}
