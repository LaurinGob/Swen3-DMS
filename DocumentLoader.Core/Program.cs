using Microsoft.Extensions.DependencyInjection;
using DocumentLoader.Core.Services;

namespace DocumentLoader.Core;

public static class ServiceRegistration
{
    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        services.AddScoped<IAccessLogService, AccessLogService>();

        return services;
    }
}