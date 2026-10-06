// Roda antes dos endpoints de dados (/clientes, /motoristas, /entregas, /notas).
// Descobre de qual visitante e a requisicao; sem sessao valida, a requisicao para aqui.
public class SessaoMiddleware
{
    private readonly RequestDelegate _proximo;

    public SessaoMiddleware(RequestDelegate proximo)
    {
        _proximo = proximo;
    }

    public async Task InvokeAsync(HttpContext contexto, GerenciadorDeSessao gerenciador)
    {
        Guid? id = GerenciadorDeSessao.LerId(contexto);

        if (id == null || await gerenciador.Ativar(id.Value) == null)
        {
            await Results.Problem(
                title: "Sessão inválida",
                detail: "Sessão de demonstração expirada ou inexistente. Chame GET /sessao para iniciar uma nova.",
                statusCode: StatusCodes.Status401Unauthorized).ExecuteAsync(contexto);
            return;
        }

        await _proximo(contexto);
    }
}
