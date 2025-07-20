using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OctaneEngineCore;

public class OctaneRequest
{
    public string Url { get; set; }
    public string? OutFile { get; set; } = null;
    public Dictionary<string, string>? Headers { get; set; } = null;

    public OctaneRequest(string url, string? outFile = null, Dictionary<string, string>? headers = null)
    {
        Url = url;
        OutFile = outFile;
        Headers = headers;
    }
}