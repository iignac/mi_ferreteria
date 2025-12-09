using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using mi_ferreteria.Security;

var builder = WebApplication.CreateBuilder(args);

// Servicios
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();
builder.Services.AddTransient<mi_ferreteria.Data.IUsuarioRepository, mi_ferreteria.Data.UsuarioRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IRolRepository, mi_ferreteria.Data.RolRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IPermisoRepository, mi_ferreteria.Data.PermisoRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IProductoRepository, mi_ferreteria.Data.ProductoRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.ICategoriaRepository, mi_ferreteria.Data.CategoriaRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IStockRepository, mi_ferreteria.Data.StockRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IClienteRepository, mi_ferreteria.Data.ClienteRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IVentaRepository, mi_ferreteria.Data.VentaRepository>();
builder.Services.AddTransient<IAuthService, AuthService>();

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

