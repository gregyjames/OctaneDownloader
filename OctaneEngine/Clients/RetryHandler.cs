/*
 * The MIT License (MIT)
 * Copyright (c) 2015 Greg James
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

#nullable enable
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OctaneEngine.Clients;

internal class RetryHandler : DelegatingHandler
{
    private readonly ILogger<RetryHandler> _log;
    private readonly int _maxRetries;
    private readonly int _retryCap;

    public RetryHandler(HttpMessageHandler innerHandler, ILoggerFactory loggerFactory, int maxRetries = 3, int retryCap = -1) : base(innerHandler)
    {
        _maxRetries = maxRetries;
        _retryCap = retryCap;
        _log = loggerFactory.CreateLogger<RetryHandler>();
        _log.LogDebug("Retry handler created with {MaxRetries} retries, with a max cap of {RetryCap}.", _maxRetries, _retryCap <= -1 ? "[DISABLED]" : _retryCap + "s");
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;
        for (var i = 0; i < _maxRetries; i++)
        {
            response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
                return response;
            
            var status = (int)response.StatusCode;

            // I consider these transient HTTP error codes
            bool shouldRetryWithBackoff = status is 408 or 429 or 500 or 502 or 503 or 504;
            
            _log.LogWarning("Client failed sending request. Retrying attempt: {Attempt}/{MaxRetries} (Status: {StatusCode})", i + 1, _maxRetries, response.StatusCode);

            if (shouldRetryWithBackoff)
            {
                var delayTime = _retryCap <= -1 ? Math.Pow(2, i) : Math.Min(Math.Pow(2, i), _retryCap);
                _log.LogDebug("Transient status of {StatusCode} waiting {delay}s before trying again.", status, delayTime);
                var delay = TimeSpan.FromSeconds(delayTime);
                await Task.Delay(delay, cancellationToken);
            }
        }

        Debug.Assert(response != null, nameof(response) + " != null");

        if (!response.IsSuccessStatusCode) _log.LogError("HTTP Response code unsuccessful");

        return response;
    }
}