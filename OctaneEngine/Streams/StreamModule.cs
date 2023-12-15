using Autofac;
using Microsoft.Extensions.Logging;
using OctaneEngine;

namespace OctaneEngineCore.Streams;

public class StreamModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<IStream>(ctx =>
        {
            var cfg = ctx.Resolve<OctaneConfiguration>();
            if (cfg.BytesPerSecond == 1)
            {
                return new NormalStream();
            }

            return new ThrottleStream(ctx.Resolve<ILoggerFactory>());
        }).As<IStream>().InstancePerDependency();
    }
}