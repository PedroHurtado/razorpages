using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SIREI.Dominio;
using SIREI.Web.Infraestructura;

namespace SIREI.Web.Pages.Incidencias;

public class ConsultaModel(IIncidenciaServicio incidencias, TimeProvider reloj) : PageModel
{
    public const int MaxFicherosSolucion = 3;

    [FromRoute]
    public int Id { get; set; }

    /// <summary>
    /// Aviso que sobrevive a la redirección cuando no hay JavaScript (TempData).
    /// No se usa [TempData]: leería TempData en cada petición y la respuesta llevaría un Set-Cookie que impide cachearla.
    /// </summary>
    public string? AvisoPendiente
    {
        get => this.LeerTempDataSiHayCookie(nameof(AvisoPendiente));
        set => TempData[nameof(AvisoPendiente)] = value;
    }

    public IncidenciaDetalle Incidencia { get; private set; } = default!;

    public DateTimeOffset Ahora => reloj.GetLocalNow();

    public string? AvisoTexto { get; private set; }

    /// <summary>"usuario" o "interno".</summary>
    public string Pestana { get; private set; } = "usuario";

    public bool ErrorSolucion { get; private set; }

    /// <summary>El historial va como swap fuera de banda en la respuesta htmx.</summary>
    public bool HistorialFueraDeBanda { get; private set; }

    /// <summary>Hay cambios sin guardar (al volver con error de validación).</summary>
    public bool Sucio { get; private set; }

    public bool Finalizada => Incidencia.Estado == EstadoIncidencia.Finalizada;

    public string Actualizada => Presentacion.Cuando(Incidencia.FechaActualizacion, Ahora);

    public string EstadoNotas => Incidencia.NotasGuardadas is { } f
        ? $"Notas guardadas a las {Presentacion.Hora(f)}"
        : "Las notas no se han guardado todavía";

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await CargarAsync())
        {
            return NotFound();
        }

        AvisoTexto = AvisoPendiente;
        return Page();
    }

    public async Task<IActionResult> OnPostMensajeAsync(string? texto)
    {
        if (!await CargarAsync())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(texto) || Finalizada)
        {
            return BadRequest();
        }

        await incidencias.EnviarMensajeAsync(Id, texto);
        if (!Request.EsHtmx())
        {
            return RedirectToPage(new { Id });
        }

        await CargarAsync();
        HistorialFueraDeBanda = true;
        return Partial("_MensajeEnviado", this);
    }

    public async Task<IActionResult> OnPostNotasAsync(string? notas)
    {
        if (!await CargarAsync())
        {
            return NotFound();
        }

        await incidencias.GuardarNotasAsync(Id, notas ?? "");
        await CargarAsync();
        return Content(System.Net.WebUtility.HtmlEncode(EstadoNotas), "text/html");
    }

    public async Task<IActionResult> OnPostBorradorAsync(string? cierre, string? notas, string? sol, bool salir)
    {
        if (!await CargarAsync())
        {
            return NotFound();
        }

        await incidencias.GuardarBorradorAsync(Id, new GestionIncidencia(cierre ?? "", notas ?? "", sol ?? ""));
        var aviso = salir ? "Borrador guardado. Ya puedes volver al listado." : "Borrador guardado.";
        if (!Request.EsHtmx())
        {
            AvisoPendiente = aviso;
            return RedirectToPage(new { Id });
        }

        await CargarAsync();
        AvisoTexto = aviso;
        return Partial("_BorradorGuardado", this);
    }

    public async Task<IActionResult> OnPostFinalizarAsync(string? cierre, string? notas, string? sol, List<IFormFile> ficherosSolucion)
    {
        if (!await CargarAsync())
        {
            return NotFound();
        }

        if (Finalizada)
        {
            return RedirectToPage(new { Id });
        }

        var gestion = new GestionIncidencia(cierre ?? "", notas ?? "", sol ?? "");
        if (string.IsNullOrWhiteSpace(sol))
        {
            // Sin JavaScript se llega aquí: se vuelve a pintar con lo escrito y el error en la pestaña interna.
            Incidencia = Incidencia with { ComentarioCierre = gestion.ComentarioCierre, NotasInternas = gestion.NotasInternas, Solucion = "" };
            Pestana = "interno";
            ErrorSolucion = true;
            Sucio = true;
            return Page();
        }

        var adjuntos = ficherosSolucion.Take(MaxFicherosSolucion)
            .Select(f => new Adjunto(Path.GetFileName(f.FileName), f.Length))
            .ToList();
        await incidencias.FinalizarAsync(Id, gestion, adjuntos);
        AvisoPendiente = "Incidencia finalizada. Se ha avisado al usuario.";
        return RedirectToPage(new { Id });
    }

    private async Task<bool> CargarAsync()
    {
        var incidencia = await incidencias.ObtenerAsync(Id);
        if (incidencia is null)
        {
            return false;
        }

        Incidencia = incidencia;
        return true;
    }
}
