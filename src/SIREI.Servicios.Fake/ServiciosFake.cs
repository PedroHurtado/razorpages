using Microsoft.Extensions.DependencyInjection;
using SIREI.Dominio;

namespace SIREI.Servicios.Fake;

/// <summary>
/// Implementación en memoria. Los datos se cargan al arrancar la aplicación y se pierden al pararla.
/// </summary>
internal sealed class IncidenciaServicioFake(TimeProvider reloj) : IIncidenciaServicio
{
    private readonly Lock _cerrojo = new();
    private readonly List<IncidenciaEntidad> _incidencias = DatosIniciales.Incidencias();
    private static readonly UsuarioActual Yo = DatosIniciales.UsuarioActual;

    public Task<ResultadoBusqueda> BuscarAsync(FiltroIncidencias filtro, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            var texto = filtro.Texto?.Trim().ToLowerInvariant() ?? "";
            var filtradas = _incidencias
                .Where(i => filtro.Estado switch
                {
                    FiltroEstado.Abiertas => i.Estado != EstadoIncidencia.Finalizada,
                    FiltroEstado.Pendiente => i.Estado == EstadoIncidencia.Pendiente,
                    FiltroEstado.EnCurso => i.Estado == EstadoIncidencia.EnCurso,
                    FiltroEstado.Finalizada => i.Estado == EstadoIncidencia.Finalizada,
                    _ => true
                })
                .Where(i => filtro.Prioridad is null || i.Prioridad == filtro.Prioridad)
                .Where(i => !filtro.Vip || Persona(i.Interesado).EsVip)
                .Where(i => texto.Length == 0
                    || i.Id.ToString().Contains(texto)
                    || i.Titulo.ToLowerInvariant().Contains(texto)
                    || i.Interesado.Contains(texto))
                .OrderByDescending(i => i.FechaEntrada)
                .ThenByDescending(i => i.Id)
                .ToList();

            var tamano = FiltroIncidencias.TamanosPermitidos.Contains(filtro.Tam) ? filtro.Tam : FiltroIncidencias.TamanoPorDefecto;
            var paginas = Math.Max(1, (int)Math.Ceiling(filtradas.Count / (double)tamano));
            var pagina = Math.Clamp(filtro.Pagina, 1, paginas);
            var filas = filtradas.Skip((pagina - 1) * tamano).Take(tamano).Select(Resumen).ToList();

            return Task.FromResult(new ResultadoBusqueda(filas, filtradas.Count, pagina, paginas, tamano));
        }
    }

    public Task<ResumenIncidencias> ResumenAsync(CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            var abiertas = _incidencias.Where(i => i.Estado != EstadoIncidencia.Finalizada).ToList();
            return Task.FromResult(new ResumenIncidencias(
                abiertas.Count(i => i.Estado == EstadoIncidencia.Pendiente),
                abiertas.Count(i => i.Estado == EstadoIncidencia.EnCurso),
                abiertas.Count(i => i.Prioridad == Prioridad.Critica),
                abiertas.Count(i => Persona(i.Interesado).EsVip)));
        }
    }

    public Task<IncidenciaDetalle?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            var i = Buscar(id);
            return Task.FromResult(i is null ? null : Detalle(i));
        }
    }

    public Task<bool> AsignarmeAsync(int id, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            var i = Buscar(id);
            if (i is null || i.Estado != EstadoIncidencia.Pendiente)
            {
                return Task.FromResult(false);
            }

            i.Asignado = Yo.Usuario;
            i.Estado = EstadoIncidencia.EnCurso;
            i.Registrar($"Asignada a {Yo.Nombre} · En curso", Ahora());
            return Task.FromResult(true);
        }
    }

    public Task<int> CrearAsync(NuevaIncidencia nueva, CancellationToken ct = default)
    {
        lock (_cerrojo)
        {
            var ahora = Ahora();
            var incidencia = new IncidenciaEntidad
            {
                Id = _incidencias.Max(i => i.Id) + 1,
                Titulo = nueva.Titulo.Trim(),
                Descripcion = nueva.Descripcion.Trim(),
                Categoria = nueva.Categoria,
                Interesado = nueva.Interesado,
                Solicitante = Yo.NombreCompleto,
                Prioridad = nueva.Prioridad,
                Estado = EstadoIncidencia.Pendiente,
                FechaEntrada = ahora,
                FechaActualizacion = ahora,
                Adjuntos = [.. nueva.Adjuntos]
            };
            incidencia.Historial.Add(new EventoHistorial($"Creada por {Yo.Nombre}", ahora));
            _incidencias.Add(incidencia);
            return Task.FromResult(incidencia.Id);
        }
    }

    public Task EnviarMensajeAsync(int id, string texto, CancellationToken ct = default) =>
        Modificar(id, i =>
        {
            var ahora = Ahora();
            i.Mensajes.Add(new Mensaje($"{Yo.Nombre} (CAU)", ahora, texto.Trim()));
            i.Registrar("Mensaje enviado al usuario", ahora);
        });

    public Task GuardarNotasAsync(int id, string notas, CancellationToken ct = default) =>
        Modificar(id, i =>
        {
            i.NotasInternas = notas;
            i.NotasGuardadas = Ahora();
        });

    public Task GuardarBorradorAsync(int id, GestionIncidencia gestion, CancellationToken ct = default) =>
        Modificar(id, i =>
        {
            var ahora = Ahora();
            Aplicar(i, gestion);
            i.NotasGuardadas = ahora;
            i.Registrar("Borrador guardado", ahora);
        });

    public Task FinalizarAsync(int id, GestionIncidencia gestion, IReadOnlyList<Adjunto> adjuntosSolucion, CancellationToken ct = default) =>
        Modificar(id, i =>
        {
            Aplicar(i, gestion);
            i.AdjuntosSolucion = [.. adjuntosSolucion];
            i.Estado = EstadoIncidencia.Finalizada;
            i.Registrar($"Finalizada por {Yo.Nombre}", Ahora());
        });

    private Task Modificar(int id, Action<IncidenciaEntidad> cambio)
    {
        lock (_cerrojo)
        {
            var i = Buscar(id) ?? throw new KeyNotFoundException($"No existe la incidencia {id}.");
            cambio(i);
        }
        return Task.CompletedTask;
    }

    private static void Aplicar(IncidenciaEntidad i, GestionIncidencia gestion)
    {
        i.ComentarioCierre = gestion.ComentarioCierre;
        i.NotasInternas = gestion.NotasInternas;
        i.Solucion = gestion.Solucion;
    }

    private DateTimeOffset Ahora() => reloj.GetLocalNow();

    private IncidenciaEntidad? Buscar(int id) => _incidencias.FirstOrDefault(i => i.Id == id);

    private static Persona Persona(string usuario) =>
        DatosIniciales.Personas.FirstOrDefault(p => p.Usuario == usuario)
        ?? new Persona(usuario, usuario, usuario, usuario[..Math.Min(2, usuario.Length)].ToUpperInvariant(), false, "[EXT]");

    private static string? NombreGestor(string? usuario) =>
        usuario is null ? null : DatosIniciales.Gestores.FirstOrDefault(g => g.Usuario == usuario)?.Nombre ?? usuario;

    private static IncidenciaResumen Resumen(IncidenciaEntidad i) => new(
        i.Id, i.Titulo, i.Interesado, Persona(i.Interesado).EsVip, i.FechaEntrada, i.Estado, i.Prioridad,
        NombreGestor(i.Asignado), i.Asignado == Yo.Usuario);

    private static IncidenciaDetalle Detalle(IncidenciaEntidad i) => new(
        i.Id, i.Titulo, i.Descripcion, i.Categoria, i.Prioridad, i.Estado, Persona(i.Interesado), i.Solicitante,
        NombreGestor(i.Asignado), i.Asignado == Yo.Usuario, i.FechaEntrada, i.FechaActualizacion,
        [.. i.Adjuntos], [.. i.Mensajes], [.. i.Historial],
        i.ComentarioCierre, i.NotasInternas, i.NotasGuardadas, i.Solucion, [.. i.AdjuntosSolucion]);
}

internal sealed class PersonaServicioFake : IPersonaServicio
{
    public Task<IReadOnlyList<Persona>> BuscarAsync(string? texto, CancellationToken ct = default)
    {
        var q = texto?.Trim().ToLowerInvariant() ?? "";
        IReadOnlyList<Persona> personas = DatosIniciales.Personas
            .Where(p => q.Length == 0 || p.NombreCompleto.ToLowerInvariant().Contains(q) || p.Usuario.Contains(q))
            .ToList();
        return Task.FromResult(personas);
    }

    public Task<Persona?> ObtenerAsync(string usuario, CancellationToken ct = default) =>
        Task.FromResult(DatosIniciales.Personas.FirstOrDefault(p => p.Usuario == usuario));
}

internal sealed class CatalogoServicioFake : ICatalogoServicio
{
    public IReadOnlyList<string> Categorias => DatosIniciales.Categorias;
}

internal sealed class UsuarioActualServicioFake : IUsuarioActualServicio
{
    public UsuarioActual Usuario => DatosIniciales.UsuarioActual;
}

public static class RegistroServiciosFake
{
    /// <summary>Registra los servicios fake. El almacén es único para toda la aplicación.</summary>
    public static IServiceCollection AddServiciosFake(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIncidenciaServicio, IncidenciaServicioFake>();
        services.AddSingleton<IPersonaServicio, PersonaServicioFake>();
        services.AddSingleton<ICatalogoServicio, CatalogoServicioFake>();
        services.AddSingleton<IUsuarioActualServicio, UsuarioActualServicioFake>();
        return services;
    }
}
