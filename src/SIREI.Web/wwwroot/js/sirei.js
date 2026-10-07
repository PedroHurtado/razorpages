// Comportamiento común de SIREI. Sin dependencias, salvo htmx.
(function () {
  'use strict';

  var focoPendiente = null;

  /**
   * Da el foco al primer selector que exista y se pueda enfocar.
   * Admite una lista de alternativas separada por "|": "#ver-1507|#resumen".
   */
  function enfocar(selectores) {
    var lista = selectores.split('|');
    for (var i = 0; i < lista.length; i++) {
      var el = document.querySelector(lista[i]);
      if (el && !el.disabled && !el.hidden && el.offsetParent !== null) {
        if (el.tabIndex < 0 && !el.hasAttribute('tabindex')) {
          el.setAttribute('tabindex', '-1');
        }
        el.focus();
        return true;
      }
    }
    return false;
  }

  function anunciar(texto) {
    var region = document.getElementById('anuncio');
    if (!region) return;
    region.textContent = '';
    // Pequeña espera para que el lector de pantalla note el cambio aunque el texto se repita
    setTimeout(function () { region.textContent = texto; }, 50);
  }

  window.sirei = { enfocar: enfocar, anunciar: anunciar };

  // ----- htmx: token antiforgery, aria-busy y foco tras el swap -----

  document.addEventListener('htmx:configRequest', function (e) {
    var meta = document.querySelector('meta[name="csrf-token"]');
    if (meta) e.detail.headers['RequestVerificationToken'] = meta.content;
  });

  document.addEventListener('htmx:beforeRequest', function (e) {
    var destino = e.detail.target;
    if (destino) destino.setAttribute('aria-busy', 'true');
    var foco = e.detail.elt.getAttribute('data-foco');
    if (foco) focoPendiente = foco;
  });

  document.addEventListener('htmx:afterRequest', function (e) {
    var destino = e.detail.target;
    if (destino) destino.removeAttribute('aria-busy');
    if (!e.detail.successful) {
      focoPendiente = null;
      anunciar('No se ha podido completar la acción. Inténtalo de nuevo.');
    }
  });

  document.addEventListener('htmx:afterSettle', function () {
    if (focoPendiente) {
      var foco = focoPendiente;
      focoPendiente = null;
      enfocar(foco);
    }
  });

  // No reescribir una región viva si el texto no cambia: evita que el lector lo repita en cada refresco
  document.addEventListener('htmx:oobBeforeSwap', function (e) {
    var destino = e.detail.target;
    var nuevo = e.detail.fragment;
    if (destino && nuevo && destino.hasAttribute('aria-live') &&
        destino.textContent.trim() === nuevo.textContent.trim()) {
      e.preventDefault();
    }
  });

  // ----- Avisos -----

  document.addEventListener('click', function (e) {
    var cerrar = e.target.closest('[data-cerrar-aviso]');
    if (!cerrar) return;
    var aviso = cerrar.closest('.alert');
    aviso.hidden = true;
    enfocar('main h1');
  });

  // ----- Contadores de caracteres: data-contador="id-del-texto" data-plantilla="Quedan {n} caracteres" -----

  document.addEventListener('input', function (e) {
    var campo = e.target;
    if (!campo.hasAttribute || !campo.hasAttribute('data-contador')) return;
    var salida = document.getElementById(campo.getAttribute('data-contador'));
    var max = Number(campo.getAttribute('maxlength'));
    salida.textContent = campo.getAttribute('data-plantilla').replace('{n}', max - campo.value.length);
  });

  // ----- Listado: tarjetas de resumen y "Restablecer filtros" -----

  var INICIAL = { texto: '', estado: 'Abiertas', prioridad: '', vip: false };

  document.addEventListener('click', function (e) {
    var boton = e.target.closest('[data-preset]');
    if (!boton) return;
    var form = document.getElementById('filtros');
    var valores = Object.assign({}, INICIAL, JSON.parse(boton.getAttribute('data-preset')));
    form.elements.texto.value = valores.texto;
    form.elements.estado.value = valores.estado;
    form.elements.prioridad.value = valores.prioridad;
    form.elements.vip.checked = valores.vip;
    focoPendiente = boton.getAttribute('data-foco');
    htmx.trigger(form, 'change');
  });

  // Refresco automático (data-refresco-auto): se cancela si el foco está en la tabla o en las tarjetas,
  // para no quitárselo al usuario. Va aquí y no en un filtro de hx-trigger porque la CSP no permite eval.
  document.addEventListener('htmx:confirm', function (e) {
    if (!e.target.hasAttribute('data-refresco-auto')) return;
    var foco = document.activeElement;
    var resultados = document.getElementById('resultados');
    var kpis = document.getElementById('kpis');
    if ((resultados && resultados.contains(foco)) || (kpis && kpis.contains(foco))) {
      e.preventDefault();
    }
  });

  // ----- Resúmenes de errores: el enlace lleva el foco al campo -----

  document.addEventListener('click', function (e) {
    var enlace = e.target.closest('.alert-error a[href^="#"]');
    if (!enlace) return;
    var campo = document.getElementById(enlace.getAttribute('href').slice(1));
    if (!campo) return;
    e.preventDefault();
    campo.focus();
  });

  // ----- Foco inicial: avisos y confirmaciones que el lector debe oír al cargar -----

  document.addEventListener('DOMContentLoaded', function () {
    var el = document.querySelector('[data-autofoco="true"]:not([hidden])');
    if (el) el.focus();
  });
})();
