using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;

namespace ProxyServer.Server.Client
{
    internal sealed class ClientHandlerPooledObjectPolicy : PooledObjectPolicy<IClientHandler>
    {
        private readonly IServiceProvider _serviceProvider;
        
        public ClientHandlerPooledObjectPolicy(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public override IClientHandler Create()
        {
            return _serviceProvider.GetService<IClientHandler>();
        }

        public override bool Return(IClientHandler clientHandler)
        {
            if (clientHandler == null) throw new ArgumentNullException(nameof(clientHandler));
            
            if (clientHandler.HasError)
            {
                // Discard this one.
                clientHandler.Dispose();
                return false;
            }

            clientHandler.Clean();

            return true;
        }
    }
}