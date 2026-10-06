// Endpoints da sandbox de demonstracao.
public static class SessaoEndpoints
{
    public static void MapSessaoEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/sessao").WithTags("Sessão de demonstração");

        // Limite por IP: evita que alguem crie milhares de sessoes em sequencia.
        grupo.MapGet("/", Iniciar).RequireRateLimiting("sessao");
        grupo.MapPost("/reiniciar", Reiniciar);
    }

    // Devolve a sessao do visitante. Se ele ainda nao tem uma (ou ela expirou),
    // cria uma nova com os dados de exemplo.
    static async Task<IResult> Iniciar(HttpContext contexto, GerenciadorDeSessao gerenciador)
    {
        Guid? id = GerenciadorDeSessao.LerId(contexto);

        Sessao? sessao = null;
        if (id != null)
        {
            sessao = await gerenciador.Ativar(id.Value);
        }

        if (sessao == null)
        {
            sessao = await gerenciador.Criar();
        }

        // O front usa o header X-Sessao; o cookie e para o Swagger funcionar sozinho.
        contexto.Response.Cookies.Append("rota_sessao", sessao.Id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = contexto.Request.IsHttps,
            MaxAge = TimeSpan.FromHours(LimpezaDeSessoes.HorasSemUso),
        });

        return Results.Ok(new { id = sessao.Id, horasSemUsoAteExpirar = LimpezaDeSessoes.HorasSemUso });
    }

    static async Task<IResult> Reiniciar(HttpContext contexto, GerenciadorDeSessao gerenciador)
    {
        Guid? id = GerenciadorDeSessao.LerId(contexto);

        if (id == null || await gerenciador.Ativar(id.Value) == null)
        {
            return Results.Problem(detail: "Sessão expirada ou inexistente. Chame GET /sessao.", statusCode: StatusCodes.Status401Unauthorized);
        }

        await gerenciador.Reiniciar(id.Value);
        return Results.NoContent();
    }
}
