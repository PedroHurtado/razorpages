namespace SIREI.Dominio;

public interface IIncidenciaServicio
{
    Task<ResultadoBusqueda> BuscarAsync(FiltroIncidencias filtro, CancellationToken ct = default);

    /// <summary>Contadores de las incidencias abiertas (pendientes y en curso).</summary>
    Task<ResumenIncidencias> ResumenAsync(CancellationToken ct = default);

    Task<IncidenciaDetalle?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Asigna la incidencia al usuario actual y la pasa a "En curso".</summary>
    Task<bool> AsignarmeAsync(int id, CancellationToken ct = default);

    Task<int> CrearAsync(NuevaIncidencia nueva, CancellationToken ct = default);

    Task EnviarMensajeAsync(int id, string texto, CancellationToken ct = default);

    Task GuardarNotasAsync(int id, string notas, CancellationToken ct = default);

    Task GuardarBorradorAsync(int id, GestionIncidencia gestion, CancellationToken ct = default);

    Task FinalizarAsync(int id, GestionIncidencia gestion, IReadOnlyList<Adjunto> adjuntosSolucion, CancellationToken ct = default);
}

public interface IPersonaServicio
{
    Task<IReadOnlyList<Persona>> BuscarAsync(string? texto, CancellationToken ct = default);

    Task<Persona?> ObtenerAsync(string usuario, CancellationToken ct = default);
}

public interface ICatalogoServicio
{
    IReadOnlyList<string> Categorias { get; }
}

/// <summary>Usuario autenticado. Sin autenticación real, lo da el servicio fake.</summary>
public interface IUsuarioActualServicio
{
    UsuarioActual Usuario { get; }
}
