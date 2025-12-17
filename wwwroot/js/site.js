// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Marca como activo el enlace del menu segun la URL actual
document.addEventListener('DOMContentLoaded', function () {
  var links = document.querySelectorAll('.app-navbar .nav-link');
  if (!links.length) return;
  var current = location.pathname.toLowerCase();
  links.forEach(function (a) {
    var href = a.getAttribute('href');
    if (!href) return;
    try {
      var url = new URL(href, location.origin);
      var p = url.pathname.toLowerCase();
      if ((p !== '/' && current.startsWith(p)) || (current === '/' && p === '/')) {
        a.classList.add('active');
      }
    } catch (e) { /* ignore malformed URLs */ }
  });

  // Tooltip accesible para filas clickeables y enlaces a productos
  try {
    document.querySelectorAll('tr.clickable-row').forEach(function(tr){
      if (!tr.getAttribute('title')) tr.setAttribute('title','Acceder al producto');
    });
    document.querySelectorAll('a[href*="/Producto/Details"]').forEach(function(a){
      if (!a.getAttribute('title')) a.setAttribute('title','Acceder al producto');
    });
  } catch(e) { /* noop */ }

  // Advertencia de cambios sin guardar (generica para formularios)
  try {
    var forms = Array.prototype.slice.call(document.querySelectorAll('form[data-unsaved-warning="true"]'));
    if (forms.length) {
      var dirty = false;
      var markDirty = function(){ dirty = true; };
      var clearDirty = function(){ dirty = false; };
      forms.forEach(function(f){
        f.addEventListener('input', markDirty, true);
        f.addEventListener('change', markDirty, true);
        f.addEventListener('submit', clearDirty);
      });
      window.addEventListener('beforeunload', function(e){
        if (!dirty) return;
        e.preventDefault();
        e.returnValue = '';
      });
      document.addEventListener('click', function(e){
        var a = e.target.closest('a[href]');
        if (!a) return;
        if (a.hasAttribute('download')) return; // descargas
        var href = a.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('javascript:')) return;
        if (a.dataset.skipUnsavedWarning === 'true') return;
        if (!dirty) return;
        var ok = confirm('Hay cambios sin guardar. Si sales, se perderan. Deseas salir igualmente?');
        if (!ok) {
          e.preventDefault();
          e.stopPropagation();
        }
      }, true);
    }
  } catch(e) { /* noop */ }
});
