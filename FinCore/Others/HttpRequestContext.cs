using FinCore.Application.Contracts.Services;

namespace FinCore.Others
{
    public class HttpRequestContext : IRequestContext
    {
        private readonly IHttpContextAccessor _accessor;

        public HttpRequestContext(IHttpContextAccessor accessor) => _accessor = accessor;

        public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

        
    }
}
