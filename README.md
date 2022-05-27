![C#](https://github.com/gregyjames/OctaneDownloader/actions/workflows/dotnet.yml/badge.svg)
[![CodeQL](https://github.com/gregyjames/OctaneDownloader/actions/workflows/codeql-analysis.yml/badge.svg?branch=master)](https://github.com/gregyjames/OctaneDownloader/actions/workflows/codeql-analysis.yml)
[![CodeFactor](https://www.codefactor.io/repository/github/gregyjames/octanedownloader/badge)](https://www.codefactor.io/repository/github/gregyjames/octanedownloader)
[![codebeat badge](https://codebeat.co/badges/9154fd6f-ac4b-4f00-8910-66488582efcd)](https://codebeat.co/projects/github-com-gregyjames-octanedownloader-master)
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

# Usage
### Simple usage
```csharp
Engine.DownloadFile("https://speed.hetzner.de/100MB.bin", 4);
```
### Advanced usage
```csharp
Engine.DownloadFile("https://speed.hetzner.de/100MB.bin", 4, 8192, true, null!, x =>
{
  //Task completion action example
  Console.WriteLine(x ? "Done!" : "Download failed!");
},Console.WriteLine).Wait();
```

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
