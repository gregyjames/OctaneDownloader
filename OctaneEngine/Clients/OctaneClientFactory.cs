using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OctaneEngine;
using OctaneEngineCore.Clients;

namespace OctaneEngineCore;


/// <summary>
/// A factory for creating and managing HttpClient instances with efficient connection pooling
/// and lifecycle management, similar to Microsoft's IHttpClientFactory.
/// </summary>
public class OctaneClientFactory : IHttpClientFactory, IDisposable
{
    private readonly ILogger<OctaneClientFactory> _logger;
    private readonly OctaneConfiguration _configuration;
    private readonly ILoggerFactory _loggerFactory;
    readonly KeyedObjectPool<HttpClient> _httpClientPool;
    private readonly object _lockObject = new();
    private bool _disposed;

    public OctaneClientFactory(OctaneConfiguration configuration, ILoggerFactory loggerFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<OctaneClientFactory>();
        _httpClientPool = new KeyedObjectPool<HttpClient>(CreateNewClient);
        _logger.LogDebug("OctaneClientFactory initialized");
    }

    /// <summary>
    /// Creates an HttpClient with the specified name, reusing existing instances when possible.
    /// </summary>
    /// <param name="name">The name of the client to create. If null or empty, uses "default".</param>
    /// <returns>An HttpClient instance</returns>
    public HttpClient CreateClient(string name = null)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OctaneClientFactory));

        var clientName = string.IsNullOrEmpty(name) ? "OctaneClient" : name;
        var client = _httpClientPool.Rent(clientName);
        return client;
    }

    public void ReturnClient(string name, HttpClient httpClient)
    {
        _httpClientPool.Return(name, httpClient);
    }
    /// <summary>
    /// Creates a new HttpClient instance with optimized configuration for the Octane engine.
    /// </summary>
    /// <param name="clientName">The name of the client</param>
    /// <returns>A configured HttpClient</returns>
    private HttpClient CreateNewClient()
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

    /// <summary>
    /// Gets or creates a named HttpClient with specific configuration.
    /// </summary>
    /// <param name="name">The name of the client</param>
    /// <param name="configureClient">Optional action to configure the client</param>
    /// <returns>An HttpClient instance</returns>
    public HttpClient CreateClient(string name, Action<HttpClient> configureClient)
    {
        var client = CreateClient(name);
        configureClient?.Invoke(client);
        return client;
    }

    /// <summary>
    /// Gets the current number of active clients.
    /// </summary>
    public int ActiveClientCount => _httpClientPool.Count;

    /// <summary>
    /// Clears all cached clients and handlers.
    /// </summary>
    public void ClearCache()
    {
        _logger.LogInformation("Clearing HttpClient cache");

        _httpClientPool.Clear();
    }

    public void TryRemoveClient(string name)
    {
        _httpClientPool.Remove(name);
    }
    
    /// <summary>
    /// Disposes the factory and all managed resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _logger.LogDebug("Disposing OctaneClientFactory");
        _httpClientPool.Clear();
        
        lock (_lockObject)
        {
            if (_disposed) return;
            _disposed = true;
        }

        ClearCache();
        
        _logger.LogDebug("OctaneClientFactory disposed successfully");
    }
}