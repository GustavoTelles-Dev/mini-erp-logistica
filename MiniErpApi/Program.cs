using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
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
builder.Services.AddMemoryCache();   // guarda por alguns minutos as sessoes ja conferidas (menos idas ao banco)
builder.Services.AddScoped<GerenciadorDeSessao>();
builder.Services.AddHostedService<LimpezaDeSessoes>();

// Leitura de notas fiscais com IA (Gemini). A chave vem de "Gemini:ChaveApi" (user-secrets / variavel de ambiente).
builder.Services.AddSingleton<ILeitorDeNota, LeitorDeNotaGemini>();

// ---------- Limites de tamanho ----------
// Nenhuma requisicao passa de 6 MB (o maior caso e a foto da nota, ate 5 MB).
// Sem isso o padrao seria 30 MB, o que facilita sobrecarregar o servidor.
const long LimiteRequisicao = 6 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = LimiteRequisicao;
    options.AddServerHeader = false;   // nao anuncia qual servidor esta rodando
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = LimiteRequisicao;
    options.ValueLengthLimit = 64 * 1024;   // o campo "dados" da nota e um JSON pequeno
    options.ValueCountLimit = 20;
});

// ---------- JSON ----------
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Evita erro de ciclo (Entrega -> Motorista -> Entregas -> ...)
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    // Enums como texto: "EmTransito" em vez de 1
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// JSON invalido vira excecao, e o TratadorDeErros responde com uma mensagem clara (em vez de um 400 vazio).
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// Erros sempre no mesmo formato (Problem Details), sem expor detalhes internos do servidor.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeErros>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS so em desenvolvimento (para abrir o index.html direto do disco).
// Em producao o front e servido pela propria API, entao nao precisa liberar outras origens.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });
}

// ---------- Limites de uso (rate limiting) por IP ----------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Limite geral: 300 requisicoes por minuto por IP (uso normal fica muito abaixo disso)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(contexto => RateLimitPartition.GetFixedWindowLimiter(
        IpDe(contexto),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));

    // Criar sessoes: 30 a cada 10 minutos
    options.AddPolicy("sessao", contexto => RateLimitPartition.GetFixedWindowLimiter(
        IpDe(contexto),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(10) }));

    // Leitura por IA custa cota do Gemini: 10 leituras por hora
    options.AddPolicy("leitura-ia", contexto => RateLimitPartition.GetFixedWindowLimiter(
        IpDe(contexto),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromHours(1) }));

    // Salvar notas (com arquivo) ocupa espaco no banco: 20 por hora
    options.AddPolicy("envio-nota", contexto => RateLimitPartition.GetFixedWindowLimiter(
        IpDe(contexto),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) }));

    // Mensagem amigavel e especifica de cada limite (o front mostra o "detail")
    options.OnRejected = async (contexto, cancelar) =>
    {
        var requisicao = contexto.HttpContext.Request;
        string mensagem = "Muitas tentativas em pouco tempo. Aguarde alguns minutos e tente de novo.";

        if (requisicao.Path.StartsWithSegments("/notas/interpretar"))
        {
            mensagem = "Você atingiu o limite de 10 leituras por IA por hora. Preencha a nota manualmente ou tente mais tarde.";
        }
        else if (requisicao.Method == "POST" && requisicao.Path == "/notas")
        {
            mensagem = "Você atingiu o limite de 20 notas salvas por hora. Tente mais tarde.";
        }

        await Results.Problem(title: "Limite atingido", detail: mensagem, statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(contexto.HttpContext);
    };
});

// No Railway a API fica atras de um proxy. ForwardLimit = 1 usa so o IP que o proxy do Railway
// acrescentou por ultimo; um X-Forwarded-For forjado pelo visitante e ignorado.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Aplica as migrations pendentes ao iniciar: o banco sempre sobe com a estrutura certa
// (no PC e no Railway), sem precisar rodar "dotnet ef database update" na mao.
// Se o banco estiver fora do ar nesse momento, tenta de novo algumas vezes antes de desistir
// (desistindo, o Railway reinicia a API sozinho).
using (var escopo = app.Services.CreateScope())
{
    var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
    const int Tentativas = 5;

    for (int tentativa = 1; tentativa <= Tentativas; tentativa++)
    {
        try
        {
            db.Database.Migrate();
            break;
        }
        catch (Exception erro) when (tentativa < Tentativas)
        {
            app.Logger.LogWarning("Banco indisponivel ao iniciar (tentativa {Tentativa} de {Total}): {Mensagem}", tentativa, Tentativas, erro.Message);
            Thread.Sleep(TimeSpan.FromSeconds(5));
        }
    }
}

app.UseForwardedHeaders();
app.UseExceptionHandler();

// HTTPS obrigatorio no navegador depois da primeira visita (so em producao)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<CabecalhosDeSeguranca>();

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseCors();
}

// Front-end (Rota ERP) servido pela propria API a partir da pasta wwwroot.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

// Antes dos endpoints de dados, descobre a sessao do visitante (ver SessaoMiddleware).
app.UseWhen(
    contexto => contexto.Request.Method != "OPTIONS" && (
        contexto.Request.Path.StartsWithSegments("/clientes") ||
        contexto.Request.Path.StartsWithSegments("/motoristas") ||
        contexto.Request.Path.StartsWithSegments("/entregas") ||
        contexto.Request.Path.StartsWithSegments("/notas")),
    ramo => ramo.UseMiddleware<SessaoMiddleware>());

// Verificacao de saude para o Railway: confirma que a API e o banco respondem
app.MapGet("/saude", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503))
    .ExcludeFromDescription();

// Endpoints, cada grupo no seu arquivo dentro da pasta Endpoints
app.MapSessaoEndpoints();
app.MapClienteEndpoints();
app.MapMotoristaEndpoints();
app.MapEntregaEndpoints();
app.MapNotaEndpoints();

app.Run();

// IP do visitante (ja corrigido pelo UseForwardedHeaders quando atras de proxy)
static string IpDe(HttpContext contexto)
{
    return contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
}
