using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace OctaneEngineCore.ShellProgressBar;

public static class ProgressModule
{
    [ExcludeFromCodeCoverage]
    internal static void AddProgressBar(this IServiceCollection services)
    {
        services.AddTransient<ProgressBar>(provider =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            
            // Only create a ProgressBar if ShowProgress is enabled
            if (!cfg.ShowProgress)
                return null;
                
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };
            return new ProgressBar(cfg.Parts, "Downloading File...", options);
        });
    }
}