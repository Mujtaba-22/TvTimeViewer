using Microsoft.EntityFrameworkCore;
using TvTimeViewer.Data;
using TvTimeViewer.Services;
var builder = WebApplication.CreateBuilder(args);


// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<TvmazeService>();
builder.Services.AddHttpClient<OmdbService>();
builder.Services.AddScoped<DeduplicationService>();
builder.Services.AddScoped<PosterEnrichmentService>();
builder.Services.AddScoped<GenreEnrichmentService>();
builder.Services.AddSingleton<ProgressTrackingService>();

builder.Services.AddHttpClient("tmdb", client =>
{
    client.Timeout = TimeSpan.FromSeconds(8);
});

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseStaticFiles();


app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
