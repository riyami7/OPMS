 using Microsoft.Extensions.DependencyInjection;
using OPMS.HttpApi.ExternalApiClients.Jund;
using Refit;

namespace OPMS.HttpApi.ExternalApiClients
{

    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddJundApi(this IServiceCollection services)
        {
            services.AddTransient<JundAuthHeaderHandler>();
            services.AddRefitClient<IJundApi>();

            services.AddScoped<IJundClient, JundClient>();
            return services;
        }

     

    }
}
