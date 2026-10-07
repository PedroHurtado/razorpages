// Nueva incidencia: validación en el navegador, buscador del interesado y adjuntos.
(function () {
  'use strict';

  var form = document.getElementById('form-nueva');
  if (!form) return; // pantalla de confirmación

  // ----- Validación -----
  // El servidor valida igual (DataAnnotations); aquí se evita perder los adjuntos en un envío fallido.

  var resumen = document.getElementById('resumen-errores');
  var campos = Array.prototype.slice.call(form.querySelectorAll('[data-obligatorio]'));

  function validar() {
    var errores = 0;
    campos.forEach(function (campo) {
      var invalido = campo.value.trim() === '';
      var idError = campo.getAttribute('data-obligatorio');
      var describedby = campo.getAttribute('data-describedby');
      var ids = [describedby, invalido ? idError : ''].filter(Boolean).join(' ');

      campo.setAttribute('aria-invalid', invalido ? 'true' : 'false');
      if (ids) campo.setAttribute('aria-describedby', ids);
      else campo.removeAttribute('aria-describedby');
      document.getElementById(idError).hidden = !invalido;
      resumen.querySelector('[data-error-de="' + campo.id + '"]').hidden = !invalido;
      if (invalido) errores++;
    });

    document.getElementById('num-errores').textContent = errores === 1 ? '1 campo' : errores + ' campos';
    resumen.hidden = errores === 0;
    return errores;
  }

  form.addEventListener('submit', function (e) {
    form.setAttribute('data-intentado', 'true');
    if (validar() > 0) {
      e.preventDefault();
      resumen.focus();
    }
  });

  // Tras el primer intento, los errores se recalculan al escribir
  form.addEventListener('input', function (e) {
    if (form.getAttribute('data-intentado') === 'true' && e.target.hasAttribute('data-obligatorio')) validar();
  });
  form.addEventListener('change', function (e) {
    if (form.getAttribute('data-intentado') === 'true' && e.target.hasAttribute('data-obligatorio')) validar();
  });

  // ----- Interesado -----

  var botonCambiar = document.getElementById('btn-cambiar');
  var panel = document.getElementById('panel-interesado');
  var buscar = document.getElementById('f-buscar');

  botonCambiar.addEventListener('click', function () {
    var abrir = panel.hidden;
    panel.hidden = !abrir;
    botonCambiar.setAttribute('aria-expanded', String(abrir));
  });

  // Intro en el buscador no debe enviar el formulario de la incidencia
  buscar.addEventListener('keydown', function (e) {
    if (e.key === 'Enter') e.preventDefault();
  });

  panel.addEventListener('click', function (e) {
    var boton = e.target.closest('[data-persona]');
    if (!boton) return;
    var nombre = boton.getAttribute('data-nombre');
    document.getElementById('f-interesado').value = boton.getAttribute('data-persona');
    document.getElementById('interesado-ini').textContent = boton.getAttribute('data-iniciales');
    document.getElementById('interesado-nombre').textContent = nombre;
    document.getElementById('interesado-usuario').textContent = boton.getAttribute('data-persona');

    panel.hidden = true;
    botonCambiar.setAttribute('aria-expanded', 'false');
    if (buscar.value) {
      buscar.value = '';
      htmx.trigger(buscar, 'search');
    }
    botonCambiar.focus();
    sirei.anunciar('Interesado: ' + nombre);
  });

  // ----- Adjuntos -----

  var input = document.getElementById('f-files');
  var zona = input.closest('.drop');
  var lista = document.getElementById('lista-ficheros');
  var uso = document.getElementById('uso-ficheros');
  var error = document.getElementById('f-files-err');
  var plantilla = document.getElementById('tpl-fichero');
  var MAX_FICHEROS = Number(input.getAttribute('data-max-ficheros'));
  var MAX_BYTES = Number(input.getAttribute('data-max-bytes'));
  var ficheros = [];

  function tam(b) {
    return b >= 1048576 ? (b / 1048576).toFixed(1).replace('.', ',') + ' MB' : Math.max(1, Math.round(b / 1024)) + ' KB';
  }

  function total(lista) {
    return lista.reduce(function (a, f) { return a + f.size; }, 0);
  }

  // El input de fichero no acumula selecciones: se reconstruye su lista con DataTransfer
  function sincronizar() {
    var dt = new DataTransfer();
    ficheros.forEach(function (f) { dt.items.add(f); });
    input.files = dt.files;
  }

  function mostrarError(texto) {
    error.querySelector('span').textContent = texto;
    error.hidden = !texto;
  }

  function pintar() {
    lista.textContent = '';
    ficheros.forEach(function (f, i) {
      var li = plantilla.content.firstElementChild.cloneNode(true);
      li.querySelector('[data-nombre]').textContent = f.name;
      li.querySelector('[data-tam]').textContent = tam(f.size);
      var quitar = li.querySelector('[data-quitar]');
      quitar.setAttribute('aria-label', 'Quitar ' + f.name);
      quitar.setAttribute('data-indice', String(i));
      lista.appendChild(li);
    });
    lista.hidden = uso.hidden = ficheros.length === 0;
    uso.textContent = ficheros.length + ' de ' + MAX_FICHEROS + ' ficheros · ' + tam(total(ficheros)) + ' de 20 MB';
  }

  function anadir(nuevos) {
    var todos = ficheros.concat(Array.prototype.slice.call(nuevos));
    if (todos.length > MAX_FICHEROS) {
      mostrarError('Solo puedes adjuntar ' + MAX_FICHEROS + ' ficheros. Quita alguno antes de añadir más.');
    } else if (total(todos) > MAX_BYTES) {
      mostrarError('Los ficheros superan los 20 MB en total (' + tam(total(todos)) + ').');
    } else {
      ficheros = todos;
      mostrarError('');
      sirei.anunciar(nuevos.length === 1 ? 'Fichero añadido.' : nuevos.length + ' ficheros añadidos.');
    }
    sincronizar();
    pintar();
  }

  input.addEventListener('change', function () {
    // input.files trae solo la última selección
    var nuevos = Array.prototype.slice.call(input.files);
    if (nuevos.length) anadir(nuevos);
    else sincronizar(); // selección cancelada: se recuperan los que ya había
  });

  lista.addEventListener('click', function (e) {
    var boton = e.target.closest('[data-quitar]');
    if (!boton) return;
    var i = Number(boton.getAttribute('data-indice'));
    var nombre = ficheros[i].name;
    ficheros.splice(i, 1);
    mostrarError('');
    sincronizar();
    pintar();
    var siguiente = lista.querySelectorAll('[data-quitar]')[Math.min(i, ficheros.length - 1)];
    (siguiente || input).focus();
    sirei.anunciar('Quitado ' + nombre + '.');
  });

  ['dragenter', 'dragover'].forEach(function (tipo) {
    zona.addEventListener(tipo, function (e) { e.preventDefault(); });
  });
  zona.addEventListener('drop', function (e) {
    e.preventDefault();
    if (e.dataTransfer && e.dataTransfer.files.length) anadir(e.dataTransfer.files);
  });
})();
