using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Banco: Postgres no Supabase ----------
// A connection string vem dos user-secrets (no seu PC) ou da variavel de ambiente
// ConnectionStrings__Supabase (no Railway). Ela nunca fica no codigo nem no GitHub.
string? conexao = builder.Configuration.GetConnectionString("Supabase");
if (string.IsNullOrWhiteSpace(conexao))
{
    throw new InvalidOperationException(
        "Connection string 'Supabase' nao configurada. Rode: dotnet user-secrets set \"ConnectionStrings:Supabase\" \"...\"");
}

// Injecao de dependencia: o ASP.NET cria um AppDbContext por requisicao e entrega
// pronto para cada endpoint que pedir (substitui o "new AppDbContext()" repetido).
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(conexao));

// Sandbox de demonstracao (uma sessao isolada por visitante)
builder.Services.AddScoped<SessaoAtual>();
builder.Services.AddScoped<GerenciadorDeSessao>();
builder.Services.AddHostedService<LimpezaDeSessoes>();

// ---------- JSON ----------
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Evita erro de ciclo (Entrega -> Motorista -> Entregas -> ...)
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    // Enums como texto: "EmTransito" em vez de 1
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Erros sempre no mesmo formato (Problem Details), inclusive erros inesperados (500),
// sem expor detalhes internos do servidor.
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS: permite abrir o front de outra origem (ex.: o index.html direto no navegador).
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

// Limite de requisicoes por IP para criar sessoes (30 a cada 10 minutos).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("sessao", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));
});

// No Railway a API fica atras de um proxy; isto faz o IP real do visitante chegar ate o limitador.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Aplica as migrations pendentes ao iniciar: o banco sempre sobe com a estrutura certa
// (no PC e no Railway), sem precisar rodar "dotnet ef database update" na mao.
using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();

// Front-end (Rota ERP) servido pela propria API a partir da pasta wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

// Antes dos endpoints de dados, descobre a sessao do visitante (ver SessaoMiddleware).
app.UseWhen(
    contexto => contexto.Request.Method != "OPTIONS" && (
        contexto.Request.Path.StartsWithSegments("/clientes") ||
        contexto.Request.Path.StartsWithSegments("/motoristas") ||
        contexto.Request.Path.StartsWithSegments("/entregas")),
    ramo => ramo.UseMiddleware<SessaoMiddleware>());

// Endpoints, cada grupo no seu arquivo dentro da pasta Endpoints
app.MapSessaoEndpoints();
app.MapClienteEndpoints();
app.MapMotoristaEndpoints();
app.MapEntregaEndpoints();

app.Run();
