![C#](https://github.com/gregyjames/OctaneDownloader/actions/workflows/dotnet.yml/badge.svg)
[![CodeQL](https://github.com/gregyjames/OctaneDownloader/actions/workflows/codeql-analysis.yml/badge.svg?branch=master)](https://github.com/gregyjames/OctaneDownloader/actions/workflows/codeql-analysis.yml)
[![CodeFactor](https://www.codefactor.io/repository/github/gregyjames/octanedownloader/badge)](https://www.codefactor.io/repository/github/gregyjames/octanedownloader)
[![codebeat badge](https://codebeat.co/badges/9154fd6f-ac4b-4f00-8910-66488582efcd)](https://codebeat.co/projects/github-com-gregyjames-octanedownloader-master)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/e0366fe0406741bcbdab62354124d877)](https://www.codacy.com/gh/gregyjames/OctaneDownloader/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=gregyjames/OctaneDownloader&amp;utm_campaign=Badge_Grade)
[![NuGet latest version](https://badgen.net/nuget/v/OctaneEngineCore)](https://www.nuget.org/packages/OctaneEngineCore)
![Nuget](https://img.shields.io/nuget/dt/OctaneEngineCore)

![alt tag](https://image.ibb.co/h2tK8v/Untitled_1.png)


A high Performance C# file downloader that asyncrounously downloads files as pieces. Made as a faster, more efficent replacement to Microsoft's WebClient.Want to see the library in action? Check out [Octane YouTube Extractor](https://github.com/gregyjames/OCTANE-YoutubeExtractor)

# Installation
```sh
dotnet add package OctaneEngineCore
```

# Features
* Multipart Downloading
* Download Retry
* Progress
* Throttling
* Logging
* Proxy Support
* Pause/Resume Support
* JSON/Microsoft.Extensions.Configuration Support

# Usage
```csharp
private const string Url = "https://plugins.jetbrains.com/files/7973/281233/sonarlint-intellij-7.4.0.60471.zip?updateId=281233&pluginId=7973&family=INTELLIJ";
private static void Main()
{
     //Logging Setup
     var seriLog = new LoggerConfiguration()
          .Enrich.FromLogContext()
          .MinimumLevel.Verbose()
          .WriteTo.File("./OctaneLog.txt")
          .WriteTo.Console(theme: AnsiConsoleTheme.Sixteen)
          .CreateLogger();
     var factory = LoggerFactory.Create(logging => {
          logging.AddSerilog(seriLog);
     });

     //JSON Config Loading
     var builder = new ConfigurationBuilder();
     builder.SetBasePath(Directory.GetCurrentDirectory())
          .AddJsonFile("appsettings.json", true, true);
     var configRoot = builder.Build();
     var config = new OctaneConfiguration(configRoot, factory);
            
     //Find Optimal number of parts
     var optimalNumberOfParts = Engine.GetOptimalNumberOfParts(Url).Result;
     seriLog.Information("Optimal number of parts to download file: {OptimalNumberOfParts}", optimalNumberOfParts);
            
     seriLog.Information("Speed: {Result}", NetworkAnalyzer.GetCurrentNetworkSpeed().Result);
     seriLog.Information("Latency: {Result}", NetworkAnalyzer.GetCurrentNetworkLatency().Result);
     var pauseTokenSource = new PauseTokenSource();
     var cancelTokenSource = new CancellationTokenSource();
            
     var octaneEngine = new Engine(factory, config);
     octaneEngine.DownloadFile(Url, null, pauseTokenSource, cancelTokenSource).Wait(cancelTokenSource.Token);
        
```

# Benchmark

```
BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.3803/22H2/2022Update)
12th Gen Intel Core i7-12700K, 1 CPU, 20 logical and 12 physical cores
.NET SDK 8.0.100
[Host] : .NET 6.0.25 (6.0.2523.51912), X64 RyuJIT AVX2 [AttachedDebugger]
Job-GUGLRW : .NET 6.0.25 (6.0.2523.51912), X64 RyuJIT AVX2
Platform=X64 IterationCount=5 WarmupCount=0
```

| Method | Url | Mean | Error | StdDev |
|------------------------- |--------------------- |---------:|--------:|---------:|
| **BenchmarkOctane** | **http:(...)150MB [30]** | **8.773 s** | **1.321 s** | **0.3430 s** |
| BenchmarkOctaneLowMemory | http:(...)150MB [30] | 8.999 s | 0.5978 s | 0.0925 s |
| BenchmarkHttpClient | http:(...)150MB [30] | 8.648 s | 0.7375 s | 0.1915 s |
| **BenchmarkOctane** | **https(...)250MB [31]** | **14.335 s** | **2.095 s** | **0.5440 s** |
| BenchmarkOctaneLowMemory | https(...)250MB [31] | 14.159 s | 1.7879 s | 0.4643 s |
| BenchmarkHttpClient | https(...)250MB [31] | 15.775 s | 2.2267 s | 0.3446 s |
| **BenchmarkOctane** | **https(...)500MB [31]** | **28.262 s** | **1.876 s** | **0.2904 s** |
| BenchmarkOctaneLowMemory | https(...)500MB [31] | 27.303 s | 1.0371 s | 0.2693 s |
| BenchmarkHttpClient | https(...)500MB [31] | 31.325 s | 1.7619 s | 0.2727 s |

# License
The MIT License (MIT)

Copyright (c) 2015 Greg James

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

# Contributors
![GitHub Contributors Image](https://contrib.rocks/image?repo=gregyjames/OctaneDownloader)
