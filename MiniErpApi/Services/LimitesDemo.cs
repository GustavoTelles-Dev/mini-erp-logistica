// Todos os limites da demonstracao publica num lugar so.
// Eles protegem o banco gratuito do Supabase (500 MB) e a cota do Gemini contra abuso,
// sem atrapalhar quem esta so testando o sistema.
public static class LimitesDemo
{
    // Sessoes ativas ao mesmo tempo (cada visitante ganha uma; somem apos 6h sem uso)
    public const int MaximoSessoes = 1000;

    // Cadastros por sessao (cada tabela: clientes, motoristas, entregas)
    public const int CadastrosPorTabela = 100;

    // Notas fiscais por sessao
    public const int NotasPorSessao = 15;

    // Espaco total para arquivos de notas (somando todas as sessoes)
    public const long EspacoTotalArquivos = 150L * 1024 * 1024;

    // Tamanho maximo de um arquivo de nota (foto ou PDF)
    public const long TamanhoMaximoArquivo = 5 * 1024 * 1024;

    // Mensagem padrao quando uma tabela da sessao chega no limite
    public static string MensagemCadastroCheio(string oQue)
    {
        return $"Limite de {CadastrosPorTabela} {oQue} por sessão de demonstração atingido. Exclua algum ou use \"Restaurar dados\".";
    }
}
