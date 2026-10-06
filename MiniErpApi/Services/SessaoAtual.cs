// Guarda o Id da sessao da requisicao atual.
// E registrado como "scoped": nasce uma instancia nova a cada requisicao HTTP.
// O SessaoMiddleware preenche o Id; o AppDbContext le o Id para filtrar os dados.
public class SessaoAtual
{
    public Guid Id { get; set; }
}
