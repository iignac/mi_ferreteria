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

  // Tooltip accesible para filas clickeables y enlaces a productos
  try {
    document.querySelectorAll('tr.clickable-row').forEach(function(tr){
      if (!tr.getAttribute('title')) tr.setAttribute('title','Acceder al producto');
    });
    document.querySelectorAll('a[href*="/Producto/Details"]').forEach(function(a){
      if (!a.getAttribute('title')) a.setAttribute('title','Acceder al producto');
    });
  } catch(e) { /* noop */ }

  // Barra de búsqueda en Stock: anclada bajo el header y estilo similar a productos
  try {
    var path = location.pathname.toLowerCase();
    if (path.startsWith('/stock')) {
      var main = document.querySelector('.container > main');
      if (main) {
        var bar = document.createElement('div');
        bar.className = 'stock-search-bar position-sticky top-0';
        bar.innerHTML = "<form method='get' action='/Stock/Buscar' class='mb-2'><div class='input-group'><input type='search' name='q' class='form-control' placeholder='Buscar producto para ver movimientos (nombre, SKU, etc.)' aria-label='Buscar movimientos por producto' autocomplete='off' /><button class='btn btn-primary' type='submit'>Buscar</button></div></form>";
        main.insertBefore(bar, main.firstChild);
        // Mensaje (si viene ?smsg=...)
        var params = new URLSearchParams(location.search);
        var smsg = params.get('smsg');
        if (smsg) {
          var alert = document.createElement('div');
          alert.className = 'alert alert-warning mt-2';
          alert.textContent = smsg;
          bar.appendChild(alert);
        }
      }
    }
  } catch(e) { /* noop */ }
});
