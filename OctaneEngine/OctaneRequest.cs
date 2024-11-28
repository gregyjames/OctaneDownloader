using System;
using System.Collections.Generic;
using System.Net;

namespace OctaneEngineCore;

public class OctaneRequest
{
    public Dictionary<string, string> Headers { get; set; }
    public string URL { get; set; }
    
    public OctaneRequest()
    {
        Headers = new Dictionary<string, string>();
    }
}