namespace SIREI.Web.Infraestructura;

/// <summary>Aviso de confirmación tras una acción (alerta verde con botón de cerrar).</summary>
/// <param name="Texto">null pinta solo el hueco oculto donde htmx colocará el aviso.</param>
/// <param name="FueraDeBanda">Se envía como swap fuera de banda de htmx.</param>
/// <param name="Autofoco">Recibe el foco al cargar la página, para que lo lea el lector de pantalla.</param>
public sealed record Aviso(string? Texto, bool FueraDeBanda = false, bool Autofoco = false);
