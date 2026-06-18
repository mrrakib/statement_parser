var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<StatementParser.Services.Parsers.ParserRegistry>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<StatementParser.Services.StatementProcessingService>>();
    return new StatementParser.Services.Parsers.ParserRegistry(
    [
        new StatementParser.Services.Parsers.IbblStatementParser(),
        new StatementParser.Services.Parsers.DbblStatementParser()
    ]);
});
builder.Services.AddScoped<StatementParser.Services.Pdf.IPdfTextExtractor, StatementParser.Services.Pdf.PdfTextExtractor>();
builder.Services.AddScoped<StatementParser.Services.StatementProcessingService>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Statement}/{action=Upload}/{id?}");
app.Run();
