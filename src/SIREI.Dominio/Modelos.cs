namespace SIREI.Dominio;

/// <summary>Persona del directorio que puede ser interesada en una incidencia.</summary>
/// <param name="NombreCompleto">Formato "Apellidos, Nombre".</param>
/// <param name="NombreCorto">Formato "Nombre Apellido", para frases.</param>
public sealed record Persona(
    string Usuario,
    string NombreCompleto,
    string NombreCorto,
    string Iniciales,
    bool EsVip,
    string Extension);

/// <summary>Técnico del CAU que puede tener incidencias asignadas.</summary>
public sealed record Gestor(string Usuario, string Nombre);

public sealed record UsuarioActual(
    string Usuario,
    string Nombre,
    string NombreCompleto,
    string Iniciales,
    string Rol);

public sealed record Mensaje(string Autor, DateTimeOffset Fecha, string Texto);

public sealed record EventoHistorial(string Texto, DateTimeOffset Fecha);

public sealed record Adjunto(string Nombre, long Tamano);

/// <summary>Fila del listado.</summary>
/// <param name="AsignadoA">Nombre del gestor asignado, o null si está sin asignar.</param>
public sealed record IncidenciaResumen(
    int Id,
    string Titulo,
    string Interesado,
    bool EsVip,
    DateTimeOffset FechaEntrada,
    EstadoIncidencia Estado,
    Prioridad Prioridad,
    string? AsignadoA,
    bool AsignadaAMi);

/// <summary>Incidencia completa para la pantalla de consulta.</summary>
public sealed record IncidenciaDetalle(
    int Id,
    string Titulo,
    string Descripcion,
    string Categoria,
    Prioridad Prioridad,
    EstadoIncidencia Estado,
    Persona Interesado,
    string Solicitante,
    string? AsignadoA,
    bool AsignadaAMi,
    DateTimeOffset FechaEntrada,
    DateTimeOffset FechaActualizacion,
    IReadOnlyList<Adjunto> Adjuntos,
    IReadOnlyList<Mensaje> Mensajes,
    IReadOnlyList<EventoHistorial> Historial,
    string ComentarioCierre,
    string NotasInternas,
    DateTimeOffset? NotasGuardadas,
    string Solucion,
    IReadOnlyList<Adjunto> AdjuntosSolucion);

public sealed class FiltroIncidencias
{
    public const int TamanoPorDefecto = 5;
    public static readonly int[] TamanosPermitidos = [5, 10, 25];

    public string? Texto { get; set; }
    public FiltroEstado Estado { get; set; } = FiltroEstado.Abiertas;
    public Prioridad? Prioridad { get; set; }
    public bool Vip { get; set; }
    public int Tam { get; set; } = TamanoPorDefecto;
    public int Pagina { get; set; } = 1;
}

public sealed record ResultadoBusqueda(
    IReadOnlyList<IncidenciaResumen> Filas,
    int Total,
    int Pagina,
    int Paginas,
    int Tamano)
{
    public int Desde => Total == 0 ? 0 : (Pagina - 1) * Tamano + 1;
    public int Hasta => Math.Min(Pagina * Tamano, Total);
}

public sealed record ResumenIncidencias(int Pendientes, int EnCurso, int Criticas, int Vip);

public sealed record NuevaIncidencia(
    string Titulo,
    string Descripcion,
    string Categoria,
    Prioridad Prioridad,
    string Interesado,
    IReadOnlyList<Adjunto> Adjuntos);

public sealed record GestionIncidencia(string ComentarioCierre, string NotasInternas, string Solucion);
