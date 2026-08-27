using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AIBridge.Editor
{
    internal sealed class AIBridgeRuntimeLanDiscoveryResult
    {
        public bool Success;
        public int Count;
        public int ReachableCount;
        public int SentPackets;
        public int ScannedInterfaces;
        public string Error;
    }

    internal static partial class AIBridgeRuntimeBridgeEditorUtility
    {
        public const int DefaultLanDiscoveryTimeoutMs = 1500;
        private const int LanDiscoveryPortScanCount = 50;
        private const int MaxPort = 65535;
        private const int MinReceiveSleepMs = 10;
        private const int HealthCheckMinTimeoutMs = 500;
        private const int HealthCheckMaxTimeoutMs = 2000;
        private const string DiscoveryProtocol = "aibridge-runtime-discovery";

        public static AIBridgeRuntimeLanDiscoveryResult DiscoverLanTargets(int timeoutMs, int udpPort, string authToken)
        {
            var result = new AIBridgeRuntimeLanDiscoveryResult();
            var targets = new List<AIBridgeRuntimeLanDiscoveryTarget>();
            var sockets = new List<AIBridgeRuntimeLanDiscoverySocket>();

            try
            {
                timeoutMs = Math.Max(100, timeoutMs <= 0 ? DefaultLanDiscoveryTimeoutMs : timeoutMs);
                var startPort = Math.Max(1, Math.Min(MaxPort, udpPort <= 0 ? AIBridgeProjectSettings.DefaultRuntimeBridgeDiscoveryUdpPort : udpPort));
                var endPort = Math.Min(MaxPort, startPort + LanDiscoveryPortScanCount - 1);
                var requestId = "disc_" + Guid.NewGuid().ToString("N");
                var payload = new Dictionary<string, object>
                {
                    ["protocol"] = DiscoveryProtocol,
                    ["version"] = 1,
                    ["requestId"] = requestId
                };
                var bytes = Encoding.UTF8.GetBytes(SerializeJson(payload, pretty: false));
                var interfaces = BuildLanDiscoveryInterfacePlan();

                for (var i = 0; i < interfaces.Count; i++)
                {
                    var interfaceInfo = interfaces[i];
                    if (!interfaceInfo.Scanned)
                    {
                        continue;
                    }

                    var socket = TryCreateLanDiscoverySocket(interfaceInfo);
                    if (socket == null)
                    {
                        continue;
                    }

                    sockets.Add(socket);
                    result.ScannedInterfaces++;
                    result.SentPackets += SendLanDiscoveryPackets(socket, bytes, startPort, endPort);
                }

                ReceiveLanDiscoveryResponses(sockets, targets, requestId, timeoutMs);
                ApplyLanDiscoveryHealthChecks(targets, timeoutMs, authToken);
                targets = CollapseLanDiscoveryTargets(targets);
                targets.Sort(CompareLanDiscoveryTargets);

                var reachableTargets = targets.Where(target => target.reachable).ToList();
                WriteDiscoveryCache(reachableTargets);

                result.Success = true;
                result.Count = targets.Count;
                result.ReachableCount = reachableTargets.Count;
                return result;
            }
            catch (Exception exception)
            {
                result.Success = false;
                result.Error = exception.Message;
                return result;
            }
            finally
            {
                for (var i = 0; i < sockets.Count; i++)
                {
                    sockets[i].Dispose();
                }
            }
        }

        private static List<AIBridgeRuntimeLanDiscoveryInterfaceInfo> BuildLanDiscoveryInterfacePlan()
        {
            var results = new List<AIBridgeRuntimeLanDiscoveryInterfaceInfo>();

            try
            {
                var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
                for (var i = 0; i < networkInterfaces.Length; i++)
                {
                    var networkInterface = networkInterfaces[i];
                    IPInterfaceProperties properties;
                    try
                    {
                        properties = networkInterface.GetIPProperties();
                    }
                    catch
                    {
                        continue;
                    }

                    var unicastAddresses = properties == null ? null : properties.UnicastAddresses;
                    if (unicastAddresses == null)
                    {
                        continue;
                    }

                    foreach (UnicastIPAddressInformation addressInfo in unicastAddresses)
                    {
                        if (addressInfo == null
                            || addressInfo.Address == null
                            || addressInfo.Address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        IPAddress mask = null;
                        try
                        {
                            mask = addressInfo.IPv4Mask;
                        }
                        catch
                        {
                        }

                        var item = CreateLanDiscoveryInterfaceInfo(networkInterface, addressInfo.Address, mask);
                        item.Scanned = IsScannableLanDiscoveryInterface(item);
                        results.Add(item);
                    }
                }
            }
            catch
            {
            }

            results.Sort(CompareLanDiscoveryInterfaces);
            return results;
        }

        private static AIBridgeRuntimeLanDiscoveryInterfaceInfo CreateLanDiscoveryInterfaceInfo(
            NetworkInterface networkInterface,
            IPAddress address,
            IPAddress mask)
        {
            return new AIBridgeRuntimeLanDiscoveryInterfaceInfo
            {
                Name = networkInterface == null ? null : networkInterface.Name,
                Description = networkInterface == null ? null : networkInterface.Description,
                Type = networkInterface == null ? null : networkInterface.NetworkInterfaceType.ToString(),
                Status = networkInterface == null ? null : networkInterface.OperationalStatus.ToString(),
                LocalIp = address == null ? null : address.ToString(),
                BroadcastAddress = address == null || mask == null ? null : BuildBroadcastAddress(address, mask),
                IsVirtual = IsVirtualInterface(networkInterface, address),
                IsLoopback = address != null && IPAddress.IsLoopback(address),
                IsApipa = IsApipaAddress(address)
            };
        }

        private static bool IsScannableLanDiscoveryInterface(AIBridgeRuntimeLanDiscoveryInterfaceInfo item)
        {
            return item != null
                && string.Equals(item.Status, OperationalStatus.Up.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(item.LocalIp)
                && !item.IsLoopback
                && !item.IsApipa
                && !item.IsVirtual;
        }

        private static AIBridgeRuntimeLanDiscoverySocket TryCreateLanDiscoverySocket(AIBridgeRuntimeLanDiscoveryInterfaceInfo interfaceInfo)
        {
            try
            {
                IPAddress localAddress;
                if (interfaceInfo == null || !IPAddress.TryParse(interfaceInfo.LocalIp, out localAddress))
                {
                    return null;
                }

                var client = new UdpClient(new IPEndPoint(localAddress, 0))
                {
                    EnableBroadcast = true
                };
                return new AIBridgeRuntimeLanDiscoverySocket(client, interfaceInfo);
            }
            catch
            {
                return null;
            }
        }

        private static int SendLanDiscoveryPackets(
            AIBridgeRuntimeLanDiscoverySocket socket,
            byte[] bytes,
            int startPort,
            int endPort)
        {
            var sent = 0;
            var endpoints = BuildBroadcastEndPoints(socket.Interface);
            for (var port = startPort; port <= endPort; port++)
            {
                for (var i = 0; i < endpoints.Count; i++)
                {
                    try
                    {
                        socket.Client.Send(bytes, bytes.Length, new IPEndPoint(endpoints[i], port));
                        sent++;
                    }
                    catch
                    {
                    }
                }
            }

            return sent;
        }

        private static void ReceiveLanDiscoveryResponses(
            List<AIBridgeRuntimeLanDiscoverySocket> sockets,
            List<AIBridgeRuntimeLanDiscoveryTarget> targets,
            string requestId,
            int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(100, timeoutMs));
            while (DateTime.UtcNow < deadline)
            {
                var sawPacket = false;
                for (var i = 0; i < sockets.Count; i++)
                {
                    var socket = sockets[i];
                    while (HasPendingUdpPacket(socket.Client))
                    {
                        sawPacket = true;
                        var remote = new IPEndPoint(IPAddress.Any, 0);
                        var responseBytes = socket.Client.Receive(ref remote);
                        var target = ParseLanDiscoveryResponse(responseBytes, remote, socket.Interface, requestId);
                        if (target == null || targets.Any(existing => IsSameLanDiscoveryTarget(existing, target)))
                        {
                            continue;
                        }

                        targets.Add(target);
                    }
                }

                if (!sawPacket)
                {
                    Thread.Sleep(MinReceiveSleepMs);
                }
            }
        }

        private static bool HasPendingUdpPacket(UdpClient client)
        {
            try
            {
                return client != null && client.Available > 0;
            }
            catch
            {
                return false;
            }
        }

        private static AIBridgeRuntimeLanDiscoveryTarget ParseLanDiscoveryResponse(
            byte[] bytes,
            IPEndPoint remote,
            AIBridgeRuntimeLanDiscoveryInterfaceInfo sourceInterface,
            string requestId)
        {
            try
            {
                var json = DeserializeJson(Encoding.UTF8.GetString(bytes));
                if (!string.Equals(GetString(json, "protocol"), DiscoveryProtocol, StringComparison.Ordinal)
                    || !string.Equals(GetString(json, "requestId"), requestId, StringComparison.Ordinal))
                {
                    return null;
                }

                var targetId = GetString(json, "targetId");
                var url = NormalizeUrl(GetString(json, "reachableUrl") ?? GetString(json, "url"));
                if (string.IsNullOrWhiteSpace(url))
                {
                    var httpPort = ReadPort(GetString(json, "bindUrl") ?? GetString(json, "httpUrl"), 27182);
                    url = BuildRemoteUrl(remote, httpPort);
                }

                var platform = GetString(json, "platform");
                var isLocal = IsLocalTarget(remote, sourceInterface);
                var isVirtual = sourceInterface != null && sourceInterface.IsVirtual;

                return new AIBridgeRuntimeLanDiscoveryTarget
                {
                    targetId = targetId ?? "http",
                    source = "lan-discovery",
                    transport = "http",
                    url = url,
                    reachableUrl = url,
                    bindUrl = NormalizeUrl(GetString(json, "bindUrl") ?? GetString(json, "httpUrl")),
                    platform = platform,
                    projectName = GetString(json, "projectName"),
                    applicationVersion = GetString(json, "applicationVersion"),
                    deviceName = GetString(json, "deviceName"),
                    requiresToken = GetBool(json, "requiresToken"),
                    capabilities = GetValue(json, "capabilities"),
                    lastSeenUtc = DateTime.UtcNow.ToString("o"),
                    remoteEndPoint = remote == null ? null : remote.ToString(),
                    sourceInterface = sourceInterface == null ? null : sourceInterface.Name,
                    sourceInterfaceDescription = sourceInterface == null ? null : sourceInterface.Description,
                    sourceInterfaceAddress = sourceInterface == null ? null : sourceInterface.LocalIp,
                    sourceInterfaceBroadcast = sourceInterface == null ? null : sourceInterface.BroadcastAddress,
                    isLocal = isLocal,
                    isVirtualInterface = isVirtual,
                    targetKind = ResolveLanDiscoveryTargetKind(platform, isLocal, isVirtual)
                };
            }
            catch
            {
                return null;
            }
        }

        private static void ApplyLanDiscoveryHealthChecks(
            List<AIBridgeRuntimeLanDiscoveryTarget> targets,
            int timeoutMs,
            string authToken)
        {
            var healthTimeoutMs = Math.Min(HealthCheckMaxTimeoutMs, Math.Max(HealthCheckMinTimeoutMs, timeoutMs));
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                target.healthUrl = BuildUrl(target.url, "/aibridge/health");
                target.lastHealthCheckUtc = DateTime.UtcNow.ToString("o");

                Dictionary<string, object> health;
                string error;
                if (!TryGetLanDiscoveryHealth(target.url, healthTimeoutMs, authToken, out health, out error))
                {
                    target.reachable = false;
                    target.healthError = error;
                    continue;
                }

                target.reachable = true;
                target.healthError = null;
                target.lastSeenUtc = DateTime.UtcNow.ToString("o");
                target.targetId = GetString(health, "targetId") ?? target.targetId;
                target.platform = GetString(health, "platform") ?? target.platform;
                target.projectName = GetString(health, "productName") ?? target.projectName;
                target.applicationVersion = GetString(health, "applicationVersion") ?? target.applicationVersion;
                target.deviceName = GetString(health, "deviceName") ?? target.deviceName;
                target.bindUrl = NormalizeUrl(GetString(health, "bindUrl") ?? GetString(health, "httpUrl") ?? target.bindUrl);
                target.reachableUrl = target.url;
                target.capabilities = GetValue(health, "capabilities") ?? target.capabilities;
                target.targetKind = ResolveLanDiscoveryTargetKind(target.platform, target.isLocal, target.isVirtualInterface);
            }
        }

        public static bool TryRefreshDiscoveredTargetHealth(
            AIBridgeRuntimeDiscoveredTargetInfo target,
            int timeoutMs,
            string authToken,
            out string error)
        {
            error = null;
            var url = target == null ? null : NormalizeUrl(target.ReachableUrl ?? target.Url);
            if (string.IsNullOrWhiteSpace(url))
            {
                error = "Discovered target URL is empty.";
                return false;
            }

            Dictionary<string, object> health;
            var now = DateTime.UtcNow.ToString("o");
            var ok = TryGetLanDiscoveryHealth(url, timeoutMs, authToken, out health, out error);
            UpdateDiscoveredTargetCacheHealth(target, health, ok, now);

            target.LastHealthCheckUtc = now;
            target.Reachable = ok;
            if (!ok)
            {
                return false;
            }

            target.LastSeenUtc = now;
            target.Stale = false;
            target.AgeSeconds = 0d;
            target.TargetId = GetString(health, "targetId") ?? target.TargetId;
            target.Platform = GetString(health, "platform") ?? target.Platform;
            target.ProjectName = GetString(health, "productName") ?? target.ProjectName;
            target.ApplicationVersion = GetString(health, "applicationVersion") ?? target.ApplicationVersion;
            target.DeviceName = GetString(health, "deviceName") ?? target.DeviceName;
            target.BindUrl = NormalizeUrl(GetString(health, "bindUrl") ?? GetString(health, "httpUrl") ?? target.BindUrl);
            target.ReachableUrl = url;
            target.TargetKind = ResolveLanDiscoveryTargetKind(target.Platform, false, false);
            return true;
        }

        private static void UpdateDiscoveredTargetCacheHealth(
            AIBridgeRuntimeDiscoveredTargetInfo target,
            Dictionary<string, object> health,
            bool reachable,
            string now)
        {
            try
            {
                var cache = ReadDiscoveryCache();
                var rawTargets = GetList(cache, "targets");
                if (cache == null || rawTargets == null)
                {
                    return;
                }

                var targetUrl = NormalizeUrl(target == null ? null : target.ReachableUrl ?? target.Url);
                for (var i = 0; i < rawTargets.Count; i++)
                {
                    var item = rawTargets[i] as Dictionary<string, object>;
                    var itemUrl = NormalizeUrl(item == null ? null : GetString(item, "reachableUrl") ?? GetString(item, "url"));
                    if (item == null || !string.Equals(itemUrl, targetUrl, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    item["reachable"] = reachable;
                    item["lastHealthCheckUtc"] = now;
                    if (reachable)
                    {
                        item["lastSeenUtc"] = now;
                        item["targetId"] = GetString(health, "targetId") ?? GetString(item, "targetId");
                        item["platform"] = GetString(health, "platform") ?? GetString(item, "platform");
                        item["projectName"] = GetString(health, "productName") ?? GetString(item, "projectName");
                        item["applicationVersion"] = GetString(health, "applicationVersion") ?? GetString(item, "applicationVersion");
                        item["deviceName"] = GetString(health, "deviceName") ?? GetString(item, "deviceName");
                        item["bindUrl"] = NormalizeUrl(GetString(health, "bindUrl") ?? GetString(health, "httpUrl") ?? GetString(item, "bindUrl"));
                        item["reachableUrl"] = targetUrl;
                    }

                    cache["updatedAtUtc"] = now;
                    File.WriteAllText(GetDiscoveryCachePath(), SerializeJson(cache, pretty: true));
                    return;
                }
            }
            catch
            {
            }
        }

        private static List<AIBridgeRuntimeLanDiscoveryTarget> CollapseLanDiscoveryTargets(
            List<AIBridgeRuntimeLanDiscoveryTarget> targets)
        {
            var collapsed = new Dictionary<string, AIBridgeRuntimeLanDiscoveryTarget>(StringComparer.OrdinalIgnoreCase);
            var unnamed = new List<AIBridgeRuntimeLanDiscoveryTarget>();
            if (targets == null)
            {
                return unnamed;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var key = string.IsNullOrWhiteSpace(target.targetId)
                    ? NormalizeUrl(target.reachableUrl ?? target.url)
                    : target.targetId;
                if (string.IsNullOrWhiteSpace(key))
                {
                    unnamed.Add(target);
                    continue;
                }

                AIBridgeRuntimeLanDiscoveryTarget existing;
                if (!collapsed.TryGetValue(key, out existing) || CompareLanDiscoveryTargets(target, existing) < 0)
                {
                    collapsed[key] = target;
                }
            }

            var result = collapsed.Values.ToList();
            result.AddRange(unnamed);
            return result;
        }

        private static bool TryGetLanDiscoveryHealth(
            string baseUrl,
            int timeoutMs,
            string authToken,
            out Dictionary<string, object> health,
            out string error)
        {
            health = null;
            error = null;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(BuildUrl(baseUrl, "/aibridge/health"));
                request.Method = "GET";
                request.Timeout = Math.Max(100, timeoutMs);
                request.ReadWriteTimeout = Math.Max(100, timeoutMs);
                request.Accept = "application/json";
                if (!string.IsNullOrWhiteSpace(authToken))
                {
                    request.Headers[HttpRequestHeader.Authorization] = "Bearer " + authToken;
                }

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    var statusCode = (int)response.StatusCode;
                    if (statusCode < 200 || statusCode >= 300)
                    {
                        error = "HTTP " + statusCode.ToString(CultureInfo.InvariantCulture) + " " + response.StatusDescription;
                        return false;
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        if (stream == null)
                        {
                            error = "Empty HTTP response.";
                            return false;
                        }

                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            health = DeserializeJson(reader.ReadToEnd());
                        }
                    }
                }

                if (health == null)
                {
                    error = "Invalid HTTP health response.";
                    return false;
                }

                return true;
            }
            catch (WebException exception)
            {
                var response = exception.Response as HttpWebResponse;
                if (response == null)
                {
                    error = exception.Message;
                }
                else
                {
                    error = "HTTP " + ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture) + " " + response.StatusDescription;
                    response.Dispose();
                }

                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void WriteDiscoveryCache(List<AIBridgeRuntimeLanDiscoveryTarget> targets)
        {
            var path = GetDiscoveryCachePath();
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var cache = new Dictionary<string, object>
            {
                ["updatedAtUtc"] = DateTime.UtcNow.ToString("o"),
                ["targets"] = MergeDiscoveryCacheTargets(targets)
            };
            File.WriteAllText(path, SerializeJson(cache, pretty: true));
        }

        private static List<object> MergeDiscoveryCacheTargets(List<AIBridgeRuntimeLanDiscoveryTarget> targets)
        {
            var merged = new List<object>();
            if (targets != null)
            {
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                    {
                        merged.Add(targets[i]);
                    }
                }
            }

            var cache = ReadDiscoveryCache();
            var rawTargets = GetList(cache, "targets");
            if (rawTargets == null)
            {
                return merged;
            }

            for (var i = 0; i < rawTargets.Count; i++)
            {
                var item = rawTargets[i] as Dictionary<string, object>;
                if (item == null || IsReplacedDiscoveryCacheItem(item, targets))
                {
                    continue;
                }

                merged.Add(item);
            }

            return merged;
        }

        private static bool IsReplacedDiscoveryCacheItem(
            Dictionary<string, object> item,
            List<AIBridgeRuntimeLanDiscoveryTarget> targets)
        {
            if (item == null || targets == null)
            {
                return false;
            }

            var itemTargetId = GetString(item, "targetId") ?? "http";
            var itemUrl = NormalizeUrl(GetString(item, "reachableUrl") ?? GetString(item, "url"));
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                var targetUrl = NormalizeUrl(target.reachableUrl ?? target.url);
                if ((!string.IsNullOrWhiteSpace(itemTargetId)
                        && string.Equals(itemTargetId, target.targetId, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(itemUrl)
                        && string.Equals(itemUrl, targetUrl, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static object GetValue(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out var value))
            {
                return null;
            }

            return value;
        }

        private static string BuildBroadcastAddress(IPAddress address, IPAddress mask)
        {
            var addressBytes = address == null ? null : address.GetAddressBytes();
            var maskBytes = mask == null ? null : mask.GetAddressBytes();
            if (addressBytes == null || maskBytes == null || addressBytes.Length != 4 || maskBytes.Length != 4)
            {
                return null;
            }

            var broadcastBytes = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                broadcastBytes[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
            }

            return new IPAddress(broadcastBytes).ToString();
        }

        private static List<IPAddress> BuildBroadcastEndPoints(AIBridgeRuntimeLanDiscoveryInterfaceInfo interfaceInfo)
        {
            var addresses = new List<IPAddress> { IPAddress.Broadcast };
            IPAddress subnetBroadcast;
            if (interfaceInfo != null
                && !string.IsNullOrWhiteSpace(interfaceInfo.BroadcastAddress)
                && IPAddress.TryParse(interfaceInfo.BroadcastAddress, out subnetBroadcast)
                && !addresses.Contains(subnetBroadcast))
            {
                addresses.Add(subnetBroadcast);
            }

            return addresses;
        }

        private static bool IsVirtualInterface(NetworkInterface networkInterface, IPAddress address)
        {
            if (networkInterface == null)
            {
                return false;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Unknown)
            {
                return true;
            }

            if (IsBenchmarkingRange(address))
            {
                return true;
            }

            var text = ((networkInterface.Name ?? string.Empty) + " " + (networkInterface.Description ?? string.Empty)).ToLowerInvariant();
            var markers = new[]
            {
                "virtual",
                "vmware",
                "hyper-v",
                "virtualbox",
                "docker",
                "wsl",
                "tap",
                "tun",
                "vpn",
                "tailscale",
                "zerotier",
                "hamachi",
                "loopback",
                "bluetooth"
            };
            for (var i = 0; i < markers.Length; i++)
            {
                if (text.IndexOf(markers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsApipaAddress(IPAddress address)
        {
            var bytes = address == null ? null : address.GetAddressBytes();
            return bytes != null && bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
        }

        private static bool IsBenchmarkingRange(IPAddress address)
        {
            var bytes = address == null ? null : address.GetAddressBytes();
            return bytes != null && bytes.Length == 4 && bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19);
        }

        private static bool IsLocalTarget(IPEndPoint remote, AIBridgeRuntimeLanDiscoveryInterfaceInfo sourceInterface)
        {
            if (remote == null || remote.Address == null)
            {
                return false;
            }

            if (IPAddress.IsLoopback(remote.Address))
            {
                return true;
            }

            return sourceInterface != null
                && string.Equals(remote.Address.ToString(), sourceInterface.LocalIp, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildRemoteUrl(IPEndPoint remote, int port)
        {
            var address = remote == null || remote.Address == null ? IPAddress.Loopback : remote.Address;
            return "http://" + FormatHost(address) + ":" + port.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatHost(IPAddress address)
        {
            if (address != null && address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                return "[" + address + "]";
            }

            return address == null ? IPAddress.Loopback.ToString() : address.ToString();
        }

        private static int ReadPort(string url, int defaultPort)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Port > 0)
            {
                return uri.Port;
            }

            return defaultPort;
        }

        private static string BuildUrl(string baseUrl, string path)
        {
            return (baseUrl ?? string.Empty).TrimEnd('/') + "/" + (path ?? string.Empty).TrimStart('/');
        }

        private static string NormalizeUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');
        }

        private static string ResolveLanDiscoveryTargetKind(string platform, bool isLocal, bool isVirtualInterface)
        {
            if (isVirtualInterface)
            {
                return "virtual-interface-target";
            }

            if (IsAndroidPlatform(platform))
            {
                return "android-player";
            }

            return isLocal ? "local-player" : "remote-player";
        }

        private static bool IsAndroidPlatform(string platform)
        {
            return !string.IsNullOrWhiteSpace(platform)
                && platform.IndexOf("Android", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSameLanDiscoveryTarget(AIBridgeRuntimeLanDiscoveryTarget left, AIBridgeRuntimeLanDiscoveryTarget right)
        {
            return left != null
                && right != null
                && string.Equals(left.targetId, right.targetId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.url, right.url, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareLanDiscoveryTargets(AIBridgeRuntimeLanDiscoveryTarget left, AIBridgeRuntimeLanDiscoveryTarget right)
        {
            var reachableCompare = CompareBoolTrueFirst(left == null || left.reachable, right == null || right.reachable);
            if (reachableCompare != 0)
            {
                return reachableCompare;
            }

            var rankCompare = GetLanDiscoveryTargetPreferenceRank(left).CompareTo(GetLanDiscoveryTargetPreferenceRank(right));
            if (rankCompare != 0)
            {
                return rankCompare;
            }

            DateTimeOffset leftSeen;
            DateTimeOffset rightSeen;
            var leftHasSeen = DateTimeOffset.TryParse(left == null ? null : left.lastSeenUtc, out leftSeen);
            var rightHasSeen = DateTimeOffset.TryParse(right == null ? null : right.lastSeenUtc, out rightSeen);
            if (leftHasSeen && rightHasSeen)
            {
                return rightSeen.CompareTo(leftSeen);
            }

            if (leftHasSeen != rightHasSeen)
            {
                return leftHasSeen ? -1 : 1;
            }

            return string.Compare(left == null ? null : left.targetId, right == null ? null : right.targetId, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetLanDiscoveryTargetPreferenceRank(AIBridgeRuntimeLanDiscoveryTarget target)
        {
            if (target == null)
            {
                return 100;
            }

            if (IsAndroidPlatform(target.platform))
            {
                return 0;
            }

            if (!target.isLocal && !target.isVirtualInterface)
            {
                return 1;
            }

            if (target.isLocal)
            {
                return 2;
            }

            return target.isVirtualInterface ? 3 : 4;
        }

        private static int CompareLanDiscoveryInterfaces(
            AIBridgeRuntimeLanDiscoveryInterfaceInfo left,
            AIBridgeRuntimeLanDiscoveryInterfaceInfo right)
        {
            var scannedCompare = CompareBoolTrueFirst(left != null && left.Scanned, right != null && right.Scanned);
            if (scannedCompare != 0)
            {
                return scannedCompare;
            }

            return string.Compare(left == null ? null : left.Name, right == null ? null : right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareBoolTrueFirst(bool left, bool right)
        {
            if (left == right)
            {
                return 0;
            }

            return left ? -1 : 1;
        }

        private sealed class AIBridgeRuntimeLanDiscoveryInterfaceInfo
        {
            public string Name;
            public string Description;
            public string Type;
            public string Status;
            public string LocalIp;
            public string BroadcastAddress;
            public bool IsVirtual;
            public bool IsLoopback;
            public bool IsApipa;
            public bool Scanned;
        }

        private sealed class AIBridgeRuntimeLanDiscoveryTarget
        {
            public string targetId;
            public string source;
            public string transport;
            public string url;
            public string reachableUrl;
            public string bindUrl;
            public string platform;
            public string projectName;
            public string applicationVersion;
            public string deviceName;
            public bool requiresToken;
            public object capabilities;
            public string lastSeenUtc;
            public string lastHealthCheckUtc;
            public bool reachable;
            public string healthUrl;
            public string healthError;
            public string remoteEndPoint;
            public string sourceInterface;
            public string sourceInterfaceDescription;
            public string sourceInterfaceAddress;
            public string sourceInterfaceBroadcast;
            public bool isLocal;
            public bool isVirtualInterface;
            public string targetKind;
        }

        private sealed class AIBridgeRuntimeLanDiscoverySocket : IDisposable
        {
            public AIBridgeRuntimeLanDiscoverySocket(
                UdpClient client,
                AIBridgeRuntimeLanDiscoveryInterfaceInfo interfaceInfo)
            {
                Client = client;
                Interface = interfaceInfo;
            }

            public UdpClient Client { get; private set; }
            public AIBridgeRuntimeLanDiscoveryInterfaceInfo Interface { get; private set; }

            public void Dispose()
            {
                if (Client != null)
                {
                    Client.Close();
                    Client = null;
                }
            }
        }
    }
}
