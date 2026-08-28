namespace Api.Contracts;

/// <summary>Body of a create-reservation request. Identity is NOT here — see TenantContext.</summary>
public sealed record CreateReservationRequest(DateTimeOffset StartUtc, DateTimeOffset EndUtc);

/// <summary>
/// Who is calling, resolved from request headers.
///
/// In production both values come from verified claims on a bearer token. The headers are a
/// demo stand-in with <b>no security value</b> — any caller can set them to anything. The shape is
/// deliberately identical so swapping in real auth touches only this type. See docs/DESIGN.md §1.
/// </summary>
public sealed record TenantContext(string TenantId, string UserId)
{
    public const string TenantHeader = "X-Tenant-Id";
    public const string UserHeader = "X-User-Id";

    /// <summary>
    /// Minimal-API binding hook. Returning null makes ASP.NET reject the request with 400, so an
    /// endpoint can never run without a tenant.
    /// </summary>
    public static ValueTask<TenantContext?> BindAsync(HttpContext http)
    {
        var tenantId = http.Request.Headers[TenantHeader].ToString();
        var userId = http.Request.Headers[UserHeader].ToString();

        return ValueTask.FromResult(
            string.IsNullOrWhiteSpace(tenantId) ? null : new TenantContext(tenantId, userId));
    }
}
