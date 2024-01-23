using Autofac;
using Microsoft.Extensions.Logging;

namespace OctaneEngineCore.ColorConsoleLogger;

public class LoggerModule: Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(ctx =>
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new ColorConsoleLoggerProvider(new ColorConsoleLoggerConfiguration()));
            return loggerFactory;
        }).As<ILoggerFactory>().IfNotRegistered(typeof(ILoggerFactory)).SingleInstance();
    }
}