using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace SIREI.Web.Infraestructura;

public static class TempDataExtensions
{
    /// <summary>
    /// Lee (y consume) un valor de TempData solo si la petición trae la cookie de TempData.
    /// Leer TempData sin esa cookie añade a la respuesta un Set-Cookie de borrado, y una respuesta
    /// con Set-Cookie no se guarda en el Output Cache (ver <see cref="CacheHtmlPolicy"/>).
    /// </summary>
    public static string? LeerTempDataSiHayCookie(this PageModel pagina, string clave)
    {
        var cookie = pagina.HttpContext.RequestServices
            .GetRequiredService<IOptions<CookieTempDataProviderOptions>>().Value.Cookie.Name!;
        return pagina.Request.Cookies.ContainsKey(cookie) ? pagina.TempData[clave] as string : null;
    }
}
