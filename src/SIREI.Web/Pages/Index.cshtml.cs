using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SIREI.Dominio;
using SIREI.Web.Infraestructura;

namespace SIREI.Web.Pages;

/// <summary>Listado de incidencias (bandeja del CAU).</summary>
public class IndexModel(IIncidenciaServicio incidencias) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public FiltroIncidencias Filtro { get; set; } = new();

    /// <summary>Aviso que sobrevive a la redirección cuando no hay JavaScript.</summary>
    [TempData]
    public string? AvisoPendiente { get; set; }

    public ResultadoBusqueda Resultado { get; private set; } = default!;

    public ResumenIncidencias Resumen { get; private set; } = default!;

    /// <summary>La respuesta es un fragmento htmx: incluye los swaps fuera de banda.</summary>
    public bool EsFragmento { get; private set; }

    /// <summary>El fragmento también actualiza las tarjetas de resumen.</summary>
    public bool ActualizarResumen { get; private set; }

    public string? AvisoTexto { get; private set; }

    public string TextoResumen => Resultado.Total > 0
        ? $"Mostrando {Resultado.Desde}–{Resultado.Hasta} de {Resultado.Total} incidencias"
        : "Ninguna incidencia coincide";

    public async Task OnGetAsync()
    {
        AvisoTexto = AvisoPendiente;
        await CargarAsync();
    }

    /// <summary>Filtros, paginación y refresco automático.</summary>
    public async Task<IActionResult> OnGetResultadosAsync(bool auto)
    {
        await CargarAsync();
        EsFragmento = true;
        ActualizarResumen = auto;
        return Partial("_Resultados", this);
    }

    public async Task<IActionResult> OnPostAsignarAsync(int id)
    {
        var asignada = await incidencias.AsignarmeAsync(id);
        var aviso = asignada
            ? $"Incidencia {id} asignada a usted. Estado: En curso."
            : $"La incidencia {id} ya no está pendiente.";

        if (!Request.EsHtmx())
        {
            AvisoPendiente = aviso;
            return RedirectToPage(new
            {
                Filtro.Texto, Filtro.Estado, Filtro.Prioridad, Filtro.Vip, Filtro.Tam, Filtro.Pagina
            });
        }

        await CargarAsync();
        EsFragmento = true;
        ActualizarResumen = true;
        AvisoTexto = aviso;
        return Partial("_Resultados", this);
    }

    private async Task CargarAsync()
    {
        Resultado = await incidencias.BuscarAsync(Filtro);
        Resumen = await incidencias.ResumenAsync();
    }
}
