// Sessao de demonstracao: cada visitante ganha a sua, com uma copia dos dados de exemplo.
// Sessoes paradas por algumas horas sao apagadas pelo servico LimpezaDeSessoes.
public class Sessao
{
    public Guid Id { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime UltimoAcesso { get; set; }
}
