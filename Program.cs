var builder = WebApplication.CreateBuilder(args);

// Ajouter les services MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configurer le pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Pour servir les fichiers statiques (CSS, JS, etc.)

app.UseRouting();

app.UseAuthorization();




// Routes plus spécifiques pour éviter l'ambiguïté
app.MapControllerRoute(
    name: "vasicekCalculate",
    pattern: "Vasicek/Calculate",
    defaults: new { controller = "Vasicek", action = "Calculate" });

app.MapControllerRoute(
    name: "vasicekCompare",
    pattern: "Vasicek/CompareScenarios",
    defaults: new { controller = "Vasicek", action = "CompareScenarios" });

// Configurer la route par défaut
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();