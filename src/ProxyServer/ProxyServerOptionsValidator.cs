using Microsoft.Extensions.Options;

namespace ProxyServer
{
    public class ProxyServerOptionsValidator : IValidateOptions<ProxyServerOptions>
    {
        public ValidateOptionsResult Validate(string name, ProxyServerOptions options)
        {
            if (options is null)
                return ValidateOptionsResult.Fail("Options object is null");
            
            if (options.ConnectionLimit < 2)
                return ValidateOptionsResult.Fail($"Property '{nameof(options.ConnectionLimit)}' must be greater than 2");

            if (options.Backlog < 1)
                return ValidateOptionsResult.Fail($"Property '{nameof(options.Backlog)}' must be greater than 1");

            if (options.KeepAliveTimeout < 0)
                return ValidateOptionsResult.Fail(
                    $"Property '{nameof(options.KeepAliveTimeout)}' must be greater than 0");

            if (options.ConnectTimeout < 2)
                return ValidateOptionsResult.Fail(
                    $"Property '{nameof(options.ConnectTimeout)}' must be greater than 0");

            if (options.SendTimeout < 2)
                return ValidateOptionsResult.Fail($"Property '{nameof(options.SendTimeout)}' must be greater than 0");

            if (options.ReceiveTimeout < 2)
                return ValidateOptionsResult.Fail(
                    $"Property '{nameof(options.ReceiveTimeout)}' must be greater than 0");

            return ValidateOptionsResult.Success;
        }
    }
}