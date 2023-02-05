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
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OctaneEngine;

internal class RetryHandler : DelegatingHandler
{
    private readonly ILogger<RetryHandler> _log;

    // Strongly consider limiting the number of retries - "retry forever" is
    // probably not the most user friendly way you could respond to "the
    // network cable got pulled out."
    private readonly int _maxRetries = 3;

    public RetryHandler(HttpMessageHandler innerHandler, int maxRetries, ILoggerFactory loggerFactory) :
        base(innerHandler)
    {
        _maxRetries = maxRetries;
        _log = loggerFactory.CreateLogger<RetryHandler>();
        _log.LogInformation("Retry handler created with {MaxRetries} retries", _maxRetries);
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
            _log.LogWarning("Client failed sending request. Retrying attempt: {I}/{MaxRetries}", i, _maxRetries);
        }

        Debug.Assert(response != null, nameof(response) + " != null");

        if (response.IsSuccessStatusCode == false) _log.LogError("HTTP Response code unsuccessful");

        return response;
    }
}