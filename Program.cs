var builder = WebApplication.CreateBuilder(args);

// Agrega los servicios al contenedor.
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<mi_ferreteria.Data.IUsuarioRepository, mi_ferreteria.Data.UsuarioRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IRolRepository, mi_ferreteria.Data.RolRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IPermisoRepository, mi_ferreteria.Data.PermisoRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IProductoRepository, mi_ferreteria.Data.ProductoRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.ICategoriaRepository, mi_ferreteria.Data.CategoriaRepository>();
builder.Services.AddTransient<mi_ferreteria.Data.IStockRepository, mi_ferreteria.Data.StockRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

// Habilita rutas por atributos para APIs
app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
