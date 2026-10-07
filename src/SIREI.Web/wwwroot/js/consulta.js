// Consulta / resolución: pestañas, cambios sin guardar, mensajes, diálogos y finalización.
(function () {
  'use strict';

  var main = document.getElementById('main');

  // ----- Pestañas (patrón ARIA: flechas, Inicio y Fin; activación automática) -----

  var tabs = Array.prototype.slice.call(document.querySelectorAll('[role="tab"]'));

  function activar(tab, conFoco) {
    tabs.forEach(function (t) {
      var activa = t === tab;
      t.setAttribute('aria-selected', String(activa));
      t.tabIndex = activa ? 0 : -1;
      document.getElementById(t.getAttribute('aria-controls')).hidden = !activa;
    });
    if (conFoco) tab.focus();
  }

  tabs.forEach(function (tab, i) {
    tab.addEventListener('click', function () { activar(tab, false); });
    tab.addEventListener('keydown', function (e) {
      var destino = null;
      if (e.key === 'ArrowRight') destino = tabs[(i + 1) % tabs.length];
      else if (e.key === 'ArrowLeft') destino = tabs[(i - 1 + tabs.length) % tabs.length];
      else if (e.key === 'Home') destino = tabs[0];
      else if (e.key === 'End') destino = tabs[tabs.length - 1];
      if (destino) {
        e.preventDefault();
        activar(destino, true);
      }
    });
  });

  // ----- Cambios sin guardar -----

  var sucio = main.getAttribute('data-cambios') === 'true';
  var badgeSucio = document.getElementById('badge-sucio');
  var botonBorrador = document.getElementById('btn-borrador');

  function marcarSucio(valor) {
    sucio = valor;
    badgeSucio.hidden = !valor;
    if (botonBorrador) botonBorrador.disabled = !valor;
  }

  document.addEventListener('input', function (e) {
    if (e.target.hasAttribute('data-sucio')) marcarSucio(true);
  });
  document.addEventListener('change', function (e) {
    if (e.target.type === 'file' && e.target.hasAttribute('data-sucio')) marcarSucio(true);
  });

  // Aviso del navegador si se cierra o recarga la pestaña con cambios
  window.addEventListener('beforeunload', function (e) {
    if (sucio) {
      e.preventDefault();
      e.returnValue = '';
    }
  });

  // ----- Diálogos (<dialog> nativo: trampa de foco, Esc y fondo inerte) -----

  var dlgSalir = document.getElementById('dlg-salir');
  var dlgFinalizar = document.getElementById('dlg-finalizar');
  var abridor = null;

  function abrir(dialogo) {
    abridor = document.activeElement;
    dialogo.showModal();
  }

  [dlgSalir, dlgFinalizar].forEach(function (dialogo) {
    dialogo.addEventListener('close', function () {
      if (abridor && document.contains(abridor) && !abridor.disabled) abridor.focus();
      abridor = null;
    });
  });

  document.addEventListener('click', function (e) {
    var cerrar = e.target.closest('[data-cerrar-dialogo]');
    if (cerrar) cerrar.closest('dialog').close();
  });

  document.getElementById('btn-volver').addEventListener('click', function (e) {
    if (!sucio) return;
    e.preventDefault();
    abrir(dlgSalir);
  });

  document.getElementById('btn-salir').addEventListener('click', function () {
    sucio = false; // salida confirmada: sin aviso del navegador
  });

  // ----- Guardar borrador (barra inferior y diálogo de salida) -----

  document.addEventListener('htmx:afterRequest', function (e) {
    var id = e.detail.elt.id;
    if (!e.detail.successful) return;
    if (id === 'btn-borrador' || id === 'btn-guardar-salir') {
      marcarSucio(false);
      if (dlgSalir.open) {
        abridor = null; // el foco irá al aviso, no al botón que abrió el diálogo
        dlgSalir.close();
      }
    }
  });

  // ----- Mensajes -----

  var formMsg = document.getElementById('form-msg');
  var textoMsg = document.getElementById('f-msg');
  var enviar = document.getElementById('btn-enviar');
  var descartar = document.getElementById('btn-descartar');
  var log = document.getElementById('log');
  var finalizada = formMsg.getAttribute('data-finalizada') === 'true';

  function actualizarMensaje() {
    var vacio = textoMsg.value.trim() === '';
    enviar.disabled = vacio || finalizada;
    descartar.hidden = vacio;
  }

  textoMsg.addEventListener('input', actualizarMensaje);

  descartar.addEventListener('click', function () {
    textoMsg.value = '';
    actualizarMensaje();
    textoMsg.focus();
  });

  formMsg.addEventListener('htmx:afterRequest', function (e) {
    if (!e.detail.successful) return;
    formMsg.reset();
    actualizarMensaje();
    textoMsg.focus();
  });

  log.addEventListener('htmx:afterSettle', function () {
    log.scrollTop = log.scrollHeight;
  });

  // ----- Ficheros de la solución (máximo 3) -----

  var solFiles = document.getElementById('f-solfiles');
  solFiles.addEventListener('change', function () {
    var max = Number(solFiles.getAttribute('data-max-ficheros'));
    var elegidos = Array.prototype.slice.call(solFiles.files);
    if (elegidos.length > max) {
      var dt = new DataTransfer();
      elegidos.slice(0, max).forEach(function (f) { dt.items.add(f); });
      solFiles.files = dt.files;
      elegidos = elegidos.slice(0, max);
    }
    document.getElementById('f-solfiles-hint').textContent = elegidos.length
      ? 'Adjuntos: ' + elegidos.map(function (f) { return f.name; }).join(', ')
      : 'Ningún fichero adjunto';
  });

  // ----- Finalizar -----

  var botonFinalizar = document.getElementById('btn-finalizar');
  var sol = document.getElementById('f-sol');
  var errorSol = document.getElementById('f-sol-err');
  var tabError = document.getElementById('tab-error');

  function errorSolucion(hayError) {
    sol.setAttribute('aria-invalid', String(hayError));
    sol.setAttribute('aria-describedby', hayError ? 'f-sol-hint f-sol-err' : 'f-sol-hint');
    errorSol.hidden = !hayError;
    tabError.hidden = !hayError;
  }

  sol.addEventListener('input', function () {
    if (sol.value.trim()) errorSolucion(false);
  });

  if (botonFinalizar) {
    botonFinalizar.addEventListener('click', function () {
      if (!sol.value.trim()) {
        activar(document.getElementById('t-interno'), false);
        errorSolucion(true);
        sol.focus();
        return;
      }
      var preview = document.getElementById('cierre-preview');
      var cierre = document.getElementById('f-cierre').value.trim();
      preview.textContent = cierre || preview.getAttribute('data-vacio');
      abrir(dlgFinalizar);
    });

    document.getElementById('f-gestion').addEventListener('submit', function () {
      sucio = false; // se envía todo: sin aviso del navegador
    });
  }
})();
