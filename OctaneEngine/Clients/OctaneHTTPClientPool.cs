using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OctaneEngine.Clients;

public class OctaneHTTPClientPool: IDisposable
{
    private readonly OctaneConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ConcurrentDictionary<string, HttpClient> _items = new();
    private readonly ILogger<OctaneHTTPClientPool> _logger;
    private readonly object _lockObject = new();
    private bool _disposed;
    
    public static readonly string DEFAULT_CLIENT_NAME = "DEFAULT";
    public OctaneHTTPClientPool(OctaneConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<OctaneHTTPClientPool>();
    }

    internal void AddClientToPool(HttpClient client)
    {
        _logger.LogDebug("Adding default client to pool");
        _items.TryAdd(DEFAULT_CLIENT_NAME, client);
    }
    
    public HttpClient Rent(string key = null)
    {
        string clientName = string.IsNullOrEmpty(key) ? DEFAULT_CLIENT_NAME : key;
        var client = _items.GetOrAdd(clientName, CreateNewClient);
        return client;
    }
    
    public HttpClient Rent(string key, Action<HttpClient> configuration)
    {
        string clientName = string.IsNullOrEmpty(key) ? DEFAULT_CLIENT_NAME : key;
        var client = _items.GetOrAdd(clientName, CreateNewClient);
        configuration?.Invoke(client);
        return client;
    }
    
    public void Return(string name, HttpClient item)
    {
        _items.AddOrUpdate(name, item, (_, existing) => existing ?? item);
    }

    public int Count => _items.Count;

    public void Clear()
    {
        foreach (var key in _items.Keys)
        {
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
        _logger.LogDebug("Creating new HttpClient");
        
        var handler = CreateOptimizedHandler(_configuration);
        
        
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(30), // Reasonable timeout for large downloads
            MaxResponseContentBufferSize = Math.Max(1, _configuration.BufferSize)
        };

        // Configure default headers for better performance
        client.DefaultRequestHeaders.Add("User-Agent", "OctaneEngine/1.0");
        client.DefaultRequestHeaders.Add("Accept", "*/*");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
        
        _logger.LogDebug("HttpClient created successfully");
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
            MaxConnectionsPerServer = config.Parts * 2,
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(20),
            KeepAlivePingDelay = TimeSpan.FromSeconds(10),
            KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests
        };

        // Wrap with retry handler for resilience
        var retryHandler = new RetryHandler(socketsHandler, _loggerFactory, config.NumRetries, config.RetryCap);
        
        return retryHandler;
    }

    public int ActiveClientCount => _items.Count;
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogDebug("Disposing OctaneClientFactory");
        
        lock (_lockObject)
        {
            if (_disposed) return;
            _disposed = true;
        }

        Clear();
        
        _logger.LogDebug("OctaneClientFactory disposed successfully");
    }
}