// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Marca como activo el enlace del menú según la URL actual
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
});
