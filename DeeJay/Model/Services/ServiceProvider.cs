using System;
using System.Linq;
using System.Threading.Tasks;
using DeeJay.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace DeeJay.Model.Services
{
    internal class ServiceProvider : IServiceProvider
    {
        internal ulong GuildId { get; }
        internal ServiceCollection Services { get; }

        internal ServiceProvider(ulong guildId)
        {
            GuildId = guildId;
            Services = new ServiceCollection();

            //services
            Services.AddSingleton(new MusicService(guildId));
        }

        internal Task Reconnect() =>
            Task.WhenAll(Services.Select(service => service.ImplementationInstance)
                .OfType<IConnectedService>()
                .Select(service => service.Reconnect()));

        public object GetService(Type serviceType) =>
            Services.Select(service => service.ImplementationInstance)
                .FirstOrDefault(instance => instance.GetType() == serviceType);

        internal TService GetService<TService>() =>
            Services.Select(service => service.ImplementationInstance)
                .OfType<TService>()
                .FirstOrDefault();
    }
}