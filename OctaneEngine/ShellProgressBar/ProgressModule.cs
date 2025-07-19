using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OctaneEngine;

namespace OctaneEngineCore.ShellProgressBar;

public static class ProgressModule
{
    internal static IServiceCollection AddProgressBar(this IServiceCollection services)
    {
        services.AddTransient<ProgressBar>(provider =>
        {
            var cfg = provider.GetRequiredService<IOptions<OctaneConfiguration>>().Value;
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };
            return new ProgressBar(cfg.Parts, "Downloading File...", options);
        });
        return services;
    }
}