using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SIREI.Dominio;

namespace SIREI.Web.Pages.Incidencias;

public class NuevaModel(
    IIncidenciaServicio incidencias,
    IPersonaServicio personas,
    ICatalogoServicio catalogo,
    IUsuarioActualServicio usuarioActual) : PageModel
{
    public const int MaxFicheros = 3;
    public const long MaxBytes = 20 * 1024 * 1024;

    public const string ErrorTitulo = "Indica un título para la incidencia";
    public const string ErrorDescripcion = "Describe brevemente el problema";
    public const string ErrorCategoria = "Selecciona una categoría";

    [BindProperty]
    public EntradaNueva Input { get; set; } = new();

    [BindProperty]
    public List<IFormFile> Adjuntos { get; set; } = [];

    public IReadOnlyList<string> Categorias => catalogo.Categorias;

    public UsuarioActual Usuario => usuarioActual.Usuario;

    public Persona Interesado { get; private set; } = default!;

    public IReadOnlyList<Persona> Personas { get; private set; } = [];

    /// <summary>Incidencia recién creada: se muestra la confirmación.</summary>
    public IncidenciaDetalle? Registrada { get; private set; }

    public string? ErrorFicheros { get; private set; }

    public bool Intentado { get; private set; }

    public bool TieneError(string campo) => ModelState[$"{nameof(Input)}.{campo}"]?.Errors.Count > 0;

    public async Task<IActionResult> OnGetAsync(int? registrada)
    {
        if (registrada is { } id)
        {
            Registrada = await incidencias.ObtenerAsync(id);
            if (Registrada is null)
            {
                return NotFound();
            }
        }

        Input.Interesado = Usuario.Usuario;
        await CargarAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Categoria is not null && !Categorias.Contains(Input.Categoria))
        {
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Categoria)}", ErrorCategoria);
        }

        if (await personas.ObtenerAsync(Input.Interesado) is null)
        {
            Input.Interesado = Usuario.Usuario;
        }

        if (Adjuntos.Count > MaxFicheros)
        {
            ErrorFicheros = $"Solo puedes adjuntar {MaxFicheros} ficheros. Quita alguno antes de añadir más.";
        }
        else if (Adjuntos.Sum(a => a.Length) > MaxBytes)
        {
            ErrorFicheros = $"Los ficheros superan los 20 MB en total ({Infraestructura.Presentacion.Tamano(Adjuntos.Sum(a => a.Length))}).";
        }

        if (!ModelState.IsValid || ErrorFicheros is not null)
        {
            Intentado = true;
            await CargarAsync();
            return Page();
        }

        var id = await incidencias.CrearAsync(new NuevaIncidencia(
            Input.Titulo!, Input.Descripcion!, Input.Categoria!, Input.Prioridad, Input.Interesado,
            [.. Adjuntos.Select(a => new Adjunto(Path.GetFileName(a.FileName), a.Length))]));

        return RedirectToPage(new { registrada = id });
    }

    /// <summary>Buscador del interesado (htmx).</summary>
    public async Task<IActionResult> OnGetPersonasAsync(string? q)
    {
        Personas = await personas.BuscarAsync(q);
        return Partial("_ListaPersonas", this);
    }

    private async Task CargarAsync()
    {
        Interesado = await personas.ObtenerAsync(Registrada?.Interesado.Usuario ?? Input.Interesado)
            ?? (await personas.ObtenerAsync(Usuario.Usuario))!;
        Personas = await personas.BuscarAsync(null);
    }

    public sealed class EntradaNueva
    {
        [Required(ErrorMessage = ErrorTitulo)]
        [StringLength(120)]
        public string? Titulo { get; set; }

        [Required(ErrorMessage = ErrorDescripcion)]
        [StringLength(4000)]
        public string? Descripcion { get; set; }

        [Required(ErrorMessage = ErrorCategoria)]
        public string? Categoria { get; set; }

        public Prioridad Prioridad { get; set; } = Prioridad.Normal;

        public string Interesado { get; set; } = "";
    }
}
