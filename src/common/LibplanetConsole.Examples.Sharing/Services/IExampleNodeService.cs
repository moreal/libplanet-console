namespace LibplanetConsole.Examples;

public interface IExampleNodeService
{
    void Subscribe(string address);

    void Unsubscribe(string address);

    Task<int> GetAddressCountAsync(CancellationToken cancellationToken);

    Task<string[]> GetAddressesAsync(CancellationToken cancellationToken);
}