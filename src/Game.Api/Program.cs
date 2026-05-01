using Game.Application;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<PrototypeMatchService>();

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/api/prototype/cardiff"));
app.MapGet("/api/prototype/cardiff", (PrototypeMatchService service) =>
    Results.Ok(service.CreateCardiffPrototype()));

app.Run();

public partial class Program;
