namespace SIREI.Web.Infraestructura;

/// <summary>
/// Añade la cabecera Content-Security-Policy a las respuestas HTML.
/// La política se lee de la configuración ("ContentSecurityPolicy"); vacía o ausente, no se envía.
/// </summary>
public sealed class ContentSecurityPolicyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    // Se lee una sola vez: el middleware es singleton.
    private readonly string? _politica = configuration["ContentSecurityPolicy"];

    public Task InvokeAsync(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_politica))
        {
            return next(context);
        }

        // El Content-Type se conoce justo antes de enviar las cabeceras, no al entrar.
        context.Response.OnStarting(() =>
        {
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.Headers.ContentSecurityPolicy = _politica;
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

public static class ContentSecurityPolicyExtensions
{
    /// <summary>CSP solo en las respuestas text/html: en CSS, JS o JSON no tiene efecto.</summary>
    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app) =>
        app.UseMiddleware<ContentSecurityPolicyMiddleware>();
}
