using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OctaneEngineCore.Clients;

public class OctaneHTTPClientPool: IDisposable
{
    private readonly OctaneConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, HttpClient> _items = new();
    private readonly ILogger<OctaneHTTPClientPool> _logger;
    private readonly object _lockObject = new();
    private bool _disposed;
    private readonly int _receiveBufferSize;
    private readonly int _sendBufferSize;

    public static readonly string DEFAULT_CLIENT_NAME = "DEFAULT";
    private readonly byte[]? _windowsKeepAliveSettings;
    public OctaneHTTPClientPool(OctaneConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<OctaneHTTPClientPool>();
        _receiveBufferSize = Math.Max(1024 * 1024, _configuration.BufferSize * 128); // 1MB minimum
        _sendBufferSize = 512 * 1024; // 512 KB for sends

        if (OperatingSystem.IsWindows())
        {
            _windowsKeepAliveSettings = new byte[12];
            BitConverter.GetBytes((uint)1).CopyTo(_windowsKeepAliveSettings, 0); // Enable
            BitConverter.GetBytes((uint)30000).CopyTo(_windowsKeepAliveSettings, 4); // Time (ms)
            BitConverter.GetBytes((uint)1000).CopyTo(_windowsKeepAliveSettings, 8); // Interval (ms)
        }
    }

    internal void AddClientToPool(HttpClient client)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Adding default client to pool");
        }
        _items.TryAdd(DEFAULT_CLIENT_NAME, client);
    }
    
    public HttpClient Rent(string key = null)
    {
        string clientName = string.IsNullOrEmpty(key) ? DEFAULT_CLIENT_NAME : key;
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Renting client with name {clientName}", clientName);
        }
        var client = _items.GetOrAdd(clientName, CreateNewClient);
        return client;
    }
    
    public HttpClient Rent(string key, Action<HttpClient> configuration)
    {
        string clientName = string.IsNullOrEmpty(key) ? DEFAULT_CLIENT_NAME : key;
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Renting client with name {clientName}", clientName);
        }
        var client = _items.GetOrAdd(clientName, CreateNewClient);
        configuration?.Invoke(client);
        return client;
    }
    
    public void Return(string name, HttpClient item)
    {
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Returning client with name {clientName}", name);
        }
        _items.AddOrUpdate(name, item, (_, existing) => existing ?? item);
    }

    public int Count => _items.Count;

    public void Clear()
    {
        foreach (var key in _items.Keys)
        {
            _logger.LogTrace("Removing and disposing client with name {clientName}", key);
            if (_items.TryRemove(key, out var item))
            {
                item.Dispose();
            }
        }

        _items.Clear();
    }
    
    /// <summary>
    /// Creates a new HttpClient instance with optimized configuration for the Octane engine.
    /// </summary>
    /// <param name="clientName">The name of the client</param>
    /// <returns>A configured HttpClient</returns>
    private HttpClient CreateNewClient(string key)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Creating new HttpClient");
        }
        
        var handler = CreateOptimizedHandler(_configuration);
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30), // Reasonable timeout for large downloads
            // do not buffer the response content
            //MaxResponseContentBufferSize = Math.Max(1, _configuration.BufferSize)
        };

        // Configure default headers for better performance
        client.DefaultRequestHeaders.Add("User-Agent", "OctaneEngine/1.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        
        client.DefaultRequestVersion = System.Net.HttpVersion.Version20;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("HttpClient created successfully with send buffer size {SendBufferSize} and receive buffer size {ReceiveBufferSize}", _sendBufferSize, _receiveBufferSize);
        }
        
        return client;
    }
    
    /// <summary>
    /// Creates an optimized HttpMessageHandler with connection pooling and retry logic.
    /// </summary>
    /// <param name="config">The Octane configuration</param>
    /// <returns>An optimized HttpMessageHandler</returns>
    private HttpMessageHandler CreateOptimizedHandler(OctaneConfiguration config)
    {
        var socketsHandler = new SocketsHttpHandler
        {
            PreAuthenticate = true,
            Proxy = config.Proxy,
            UseProxy = config.UseProxy,
            MaxConnectionsPerServer = Math.Min(Math.Max(config.Parts * 4, 100), 1000),
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(30),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            KeepAlivePingDelay = TimeSpan.FromSeconds(20),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };

        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsIOS())
        {
            socketsHandler.ConnectCallback = async (context, cancellationToken) =>
            {
                Socket? socket = null;

                try
                {
                    socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                    {
                        NoDelay = true,
                        ReceiveBufferSize = _receiveBufferSize,
                        SendBufferSize = _sendBufferSize,
                        LingerState = new LingerOption(false, 0)
                    };

                    try
                    {
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseUnicastPort, true);
                        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    }
                    catch (SocketException ex) {}
                    catch (PlatformNotSupportedException ex) {}
                    
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                    if (OperatingSystem.IsWindows())
                    {
                        socket.IOControl(IOControlCode.KeepAliveValues, _windowsKeepAliveSettings, null);
                    }

                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    try
                    {
                        socket?.Dispose();
                    }
                    catch
                    {
                        // Ignore
                    }

                    throw;
                }
            };
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Skipping custom socket options on macOS/iOS due to platform socket reuse restrictions.");
            }
        }

        // Wrap with retry handler for resilience
        var retryHandler = new RetryHandler(socketsHandler, _loggerFactory, config.NumRetries, config.RetryCap);
        
        return retryHandler;
    }
    
    public int ActiveClientCount => _items.Count;
    public void Dispose()
    {
        if (_disposed) return;
        
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Disposing OctaneClientFactory");
        }
        
        lock (_lockObject)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Clear();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("OctaneClientFactory disposed successfully");
        }
    }
}