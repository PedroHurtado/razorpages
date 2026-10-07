using SIREI.Dominio;

namespace SIREI.Servicios.Fake;

/// <summary>Estado interno mutable de una incidencia dentro del almacén en memoria.</summary>
internal sealed class IncidenciaEntidad
{
    public int Id { get; init; }
    public required string Titulo { get; init; }
    public required string Descripcion { get; init; }
    public required string Categoria { get; init; }
    public required string Interesado { get; init; }
    public required string Solicitante { get; init; }
    public Prioridad Prioridad { get; init; }
    public EstadoIncidencia Estado { get; set; }
    public string? Asignado { get; set; }
    public DateTimeOffset FechaEntrada { get; init; }
    public DateTimeOffset FechaActualizacion { get; set; }
    public List<Adjunto> Adjuntos { get; init; } = [];
    public List<Mensaje> Mensajes { get; } = [];

    /// <summary>Más reciente primero.</summary>
    public List<EventoHistorial> Historial { get; } = [];

    public string ComentarioCierre { get; set; } = "";
    public string NotasInternas { get; set; } = "";
    public DateTimeOffset? NotasGuardadas { get; set; }
    public string Solucion { get; set; } = "";
    public List<Adjunto> AdjuntosSolucion { get; set; } = [];

    public void Registrar(string texto, DateTimeOffset fecha)
    {
        Historial.Insert(0, new EventoHistorial(texto, fecha));
        FechaActualizacion = fecha;
    }
}
