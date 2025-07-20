using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OctaneEngine;
using OctaneEngineCore.Clients;
using OctaneEngineCore.ShellProgressBar;

namespace OctaneEngineCore;

/// <summary>
/// Builder class for creating Octane Engine instances without dependency injection
/// </summary>
public class EngineBuilder
{
    private OctaneConfiguration _configuration;
    private ILoggerFactory _loggerFactory;
    private ProgressBar _progressBar;
    private HttpClient _client;

    /// <summary>
    /// Creates a new EngineBuilder instance
    /// </summary>
    public EngineBuilder()
    {
        _configuration = new OctaneConfiguration();
        _loggerFactory = NullLoggerFactory.Instance;
    }

    /// <summary>
    /// Sets the configuration for the engine
    /// </summary>
    public EngineBuilder WithConfiguration(OctaneConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        return this;
    }

    /// <summary>
    /// Sets the configuration using an action
    /// </summary>
    public EngineBuilder WithConfiguration(Action<OctaneConfiguration> configure)
    {
        configure?.Invoke(_configuration);
        return this;
    }

    /// <summary>
    /// Sets the logger factory
    /// </summary>
    public EngineBuilder WithLogger(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        return this;
    }

    /// <summary>
    /// Sets the progress bar (optional)
    /// </summary>
    public EngineBuilder WithProgressBar(ProgressBar progressBar)
    {
        _progressBar = progressBar;
        return this;
    }
    
    /// <summary>
    /// Sets the preconfigured HTTP Client to use (optional)
    /// </summary>
    /// <param name="httpClient">The preconfigured HTTP Client</param>
    /// <returns></returns>
    public EngineBuilder WithHttpClient(HttpClient httpClient)
    {
        _client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        return this;
    }
    
    /// <summary>
    /// Builds the Engine instance
    /// </summary>
    public IEngine Build()
    {
        // Validate and fix configuration values
        if (_configuration.Parts <= 0)
            _configuration.Parts = Environment.ProcessorCount;
        
        if (_configuration.BufferSize <= 0)
            _configuration.BufferSize = 8192;
        
        if (_configuration.NumRetries < 0)
            _configuration.NumRetries = 3;
        
        if (_configuration.BytesPerSecond <= 0)
            _configuration.BytesPerSecond = 1;
        
        // Create clients
        var octaneClient = new OctaneClient(_configuration, _client, _loggerFactory, _progressBar);
        var defaultClient = new DefaultClient(_client, _configuration);

        // Create engine
        return new Engine(_configuration, _loggerFactory);
    }

    /// <summary>
    /// Creates a new EngineBuilder with default configuration
    /// </summary>
    public static EngineBuilder Create()
    {
        return new EngineBuilder();
    }

    /// <summary>
    /// Creates a new EngineBuilder with custom configuration
    /// </summary>
    public static EngineBuilder Create(Action<OctaneConfiguration> configure)
    {
        var builder = new EngineBuilder();
        builder.WithConfiguration(configure);
        return builder;
    }

    /// <summary>
    /// Creates a new EngineBuilder with custom configuration and logger
    /// </summary>
    public static EngineBuilder Create(Action<OctaneConfiguration> configure, ILoggerFactory loggerFactory)
    {
        var builder = new EngineBuilder();
        builder.WithConfiguration(configure);
        builder.WithLogger(loggerFactory);
        return builder;
    }
} 