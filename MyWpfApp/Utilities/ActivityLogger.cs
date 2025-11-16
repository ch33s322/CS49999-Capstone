using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace MyWpfApp.Utilities
{
    public static class ActivityLogger
    {
        private static readonly object _fileLock = new object();
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings_changes.log");

        /// <summary>
        /// Write a single line to the log describing the change.
        /// </summary>
        public static void LogChange(string settingName, object oldValue, object newValue)
        {
            var message = $"Change: {settingName} from '{oldValue}' to '{newValue}'";
            WriteLogLine(message);
        }

        /// <summary>
        /// Generic action logger for operations like adding/removing printers.
        /// </summary>
        public static void LogAction(string action, string details = null)
        {
            var message = string.IsNullOrWhiteSpace(details) ? action : $"{action}: {details}";
            WriteLogLine(message);
        }

        private static void WriteLogLine(string message)
        {
            var timestamp = DateTime.UtcNow.ToString("o"); // ISO 8601 UTC
            var (ip, mac) = GetPrimaryIpAndMac();
            var line = $"{timestamp} IP:{ip} MAC:{mac} Action: {message}";

            try
            {
                lock (_fileLock)
                {
                    var dir = Path.GetDirectoryName(LogFilePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    Directory.CreateDirectory(dir);
                    File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Never throw from logger; surface to debug output for developers
                Debug.WriteLine($"ActivityLogger failed to write log: {ex}");
            }
        }

        /// <summary>
        /// Returns primary IPv4 and MAC for an "up" network interface. Falls back to DNS lookup or "Unknown".
        /// </summary>
        private static (string ip, string mac) GetPrimaryIpAndMac()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.GetIPProperties().UnicastAddresses.Any(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));

                foreach (var ni in candidates)
                {
                    var ipAddr = ni.GetIPProperties().UnicastAddresses
                        .FirstOrDefault(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.Address;
                    var macRaw = ni.GetPhysicalAddress()?.ToString();
                    string macFormatted = "Unknown";
                    if (!string.IsNullOrEmpty(macRaw))
                    {
                        // format as AA:BB:CC:DD:EE:FF
                        macFormatted = string.Join(":", Enumerable.Range(0, macRaw.Length / 2).Select(i => macRaw.Substring(i * 2, 2)));
                    }

                    return (ipAddr?.ToString() ?? "Unknown", macFormatted);
                }
            }
            catch { /* swallow - fallback below */ }

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                var ip = host.AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "Unknown";
                return (ip, "Unknown");
            }
            catch { }

            return ("Unknown", "Unknown");
        }
    }
}