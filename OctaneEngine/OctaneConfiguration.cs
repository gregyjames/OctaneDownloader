using System;
using System.Collections;

namespace OctaneEngine;

public class OctaneConfiguration
{
    private int _parts;
    private int _bufferSize;
    private bool _showProgress;
    private Action<Boolean> _doneCallback;
    private Action<Double> _progressCallback;
    private int _numRetries;
    private int _bytesPerSecond;
    
    public int Parts
    {
        get => _parts;
        set => _parts = value;
    }

    public int BufferSize
    {
        get => _bufferSize;
        set => _bufferSize = value;
    }

    public bool ShowProgress
    {
        get => _showProgress;
        set => _showProgress = value;
    }
    public Action<bool> DoneCallback
    {
        get => _doneCallback;
        set => _doneCallback = value;
    }

    public Action<double> ProgressCallback
    {
        get => _progressCallback;
        set => _progressCallback = value;
    }

    public int NumRetries
    {
        get => _numRetries;
        set => _numRetries = value;
    }

    public int BytesPerSecond
    {
        get => _bytesPerSecond;
        set => _bytesPerSecond = value;
    }

    public OctaneConfiguration()
    {
        _parts = 1;
        _bufferSize = 8096;
        _showProgress = false;
        _doneCallback = null!;
        _progressCallback = null!;
        _numRetries = 10;
        _bytesPerSecond = 1;
    }
}