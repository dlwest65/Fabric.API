using Fabric.Contracts;

namespace Fabric.API.Extensions;

public static class HttpContextExtensions
{
    public static TenantContext GetTenantContext(this HttpContext context)
    {
        return context.Items["TenantContext"] as TenantContext
            ?? throw new InvalidOperationException("TenantContext not found. Ensure ApiKeyAuthMiddleware is registered.");
    }
}
