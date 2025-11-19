
using Microsoft.Extensions.DependencyInjection;
using Shiron.Manila.Exceptions;
using Spectre.Console.Cli;

namespace Shiron.Manila.CLI.Utils;

public sealed class TypeRegistrar(IServiceCollection builder) : ITypeRegistrar {
    private readonly IServiceCollection _builder = builder;

    public ITypeResolver Build() {
        return new TypeResolver(_builder.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation) {
        _ = _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation) {
        _ = _builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory) {
        _ = _builder.AddSingleton(service, (provider) => factory());
    }
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver {
    private readonly IServiceProvider _provider = provider ?? throw new ManilaException(nameof(provider));

    public object? Resolve(Type? type) {
        return type == null ? throw new ManilaException("Type cannot be null when resolving services.") : _provider.GetService(type);
    }
}
