using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SIREI.Web.Pages;

[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public string Titulo { get; private set; } = "Se ha producido un error";

    public string Detalle { get; private set; } = "Inténtalo de nuevo. Si el problema continúa, llama al CAU: ext. 90560.";

    public void OnGet(int? codigo) => Configurar(codigo);

    public void OnPost(int? codigo) => Configurar(codigo);

    private void Configurar(int? codigo)
    {
        if (codigo == StatusCodes.Status404NotFound)
        {
            Titulo = "No se encuentra la página";
            Detalle = "La dirección no existe o la incidencia ya no está disponible.";
        }
    }
}
