namespace SIREI.Web.Infraestructura;

public static class HtmxExtensions
{
    /// <summary>La petición la ha hecho htmx (cabecera HX-Request).</summary>
    public static bool EsHtmx(this HttpRequest request) =>
        request.Headers.TryGetValue("HX-Request", out var valor) && valor == "true";
}
