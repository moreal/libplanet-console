using Libplanet.Crypto;
using LibplanetConsole.Common;

namespace LibplanetConsole.Examples;

public interface IExampleNodeContent
{
    event EventHandler<ItemEventArgs<Address>>? Subscribed;

    event EventHandler<ItemEventArgs<Address>>? Unsubscribed;

    int Count { get; }

    Task<Address[]> GetAddressesAsync(CancellationToken cancellationToken);

    void Subscribe(Address address);

    void Unsubscribe(Address address);
}