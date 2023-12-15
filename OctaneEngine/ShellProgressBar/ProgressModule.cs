using Autofac;
using OctaneEngine;

namespace OctaneEngineCore.ShellProgressBar;

public class ProgressModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(ctx =>
        {
            var cfg = ctx.Resolve<OctaneConfiguration>();
            var options = new ProgressBarOptions
            {
                ProgressBarOnBottom = false,
                BackgroundCharacter = '\u2593',
                DenseProgressBar = false,
                DisplayTimeInRealTime = false
            };

            var pbar = new ProgressBar(cfg.Parts, "Downloading File...", options);
            return pbar;
        }).As<IProgressBar>().InstancePerDependency();
    }
}