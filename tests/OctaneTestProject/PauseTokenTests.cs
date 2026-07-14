using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OctaneEngineCore;

namespace OctaneTestProject;

[TestFixture]
public class PauseTokenTests
{
    [Test]
    public async Task PauseToken_BlocksWhenPaused_ResumesOnResume()
    {
        var source = new PauseTokenSource();
        var token = source.Token;

        source.Pause();
        Assert.That(source.IsPaused, Is.True);
        Assert.That(token.IsPaused, Is.True);

        var waitTask = token.WaitWhilePausedAsync();
        Assert.That(waitTask.IsCompleted, Is.False, "Task should not be completed while paused.");

        source.Resume();

        await waitTask; // Should complete quickly now
        Assert.That(source.IsPaused, Is.False);
        Assert.That(token.IsPaused, Is.False);
    }

    [Test]
    public void PauseToken_WaitWhilePausedAsync_ThrowsWhenCanceled()
    {
        var source = new PauseTokenSource();
        var token = source.Token;
        using var cts = new CancellationTokenSource();

        source.Pause();

        var waitTask = token.WaitWhilePausedAsync(cts.Token);
        Assert.That(waitTask.IsCompleted, Is.False);

        cts.Cancel();

        var exception = Assert.ThrowsAsync<TaskCanceledException>((Func<Task>)(async () => await waitTask));
        Assert.That(exception, Is.Not.Null);
    }

    [Test]
    public async Task PauseToken_Resume_ExecutesContinuationsAsynchronously()
    {
        var source = new PauseTokenSource();
        var token = source.Token;
        source.Pause();

        int? continuationThreadId = null;
        var tcs = new TaskCompletionSource<bool>();

        var waitTask = Task.Run(async () =>
        {
            await token.WaitWhilePausedAsync();
            continuationThreadId = Environment.CurrentManagedThreadId;
            tcs.SetResult(true);
        });

        // Give it a moment to actually hit the await
        await Task.Delay(50); 
        
        var resumeThreadId = Environment.CurrentManagedThreadId;
        source.Resume();
        
        // If it ran synchronously, continuationThreadId would already be set here.
        bool ranSynchronously = continuationThreadId.HasValue;
        
        await tcs.Task; // Wait for continuation to run

        Assert.That(ranSynchronously, Is.False, "The continuation ran synchronously during the Resume() call.");
        Assert.That(continuationThreadId.HasValue, Is.True);
    }

    [Test]
    public void PauseToken_MultiplePauseResume_IsStable()
    {
        var source = new PauseTokenSource();
        
        // Hammering pause/resume shouldn't throw exceptions
        Assert.DoesNotThrow((Action)(() =>
        {
            source.Pause();
            source.Pause();
            source.Pause();
            source.Resume();
            source.Resume();
            source.Resume();
            source.Pause();
            source.Resume();
        }));
    }
}
