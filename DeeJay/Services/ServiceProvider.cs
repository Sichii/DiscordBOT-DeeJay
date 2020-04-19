using System;
using System.Collections.Generic;
using System.Linq;
using DeeJay.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DeeJay.Services
{
    internal class ServiceProvider : IServiceProvider
    {
        internal static ServiceProvider DefaultInstance = new ServiceProvider(0);
        internal ulong GuildId { get; }
        internal ServiceCollection Services { get; }

        internal IEnumerable<IConnectedService> ConnectedServices =>
            Services.Select(service => service.ImplementationInstance)
                .OfType<IConnectedService>();

        internal ServiceProvider(ulong guildId)
        {
            GuildId = guildId;
            Services = new ServiceCollection();

            //services
            Services.AddSingleton(new MusicService(guildId));
        }

        public object GetService(Type serviceType) =>
            Services.Select(service => service.ImplementationInstance)
                .FirstOrDefault(instance => instance.GetType() == serviceType);

        internal TService GetService<TService>() => (TService) GetService(typeof(TService));
    }
}