using Microsoft.Extensions.DependencyInjection;

namespace FerreTools.DependencyInjection.Services
{
    public class ConfigureServices
    {
        public static IServiceCollection AddServices(IServiceCollection services)
        {
            //services.AddScoped<ISecurityApi, SecurityApi>();
            return services;
        }
    }
}
