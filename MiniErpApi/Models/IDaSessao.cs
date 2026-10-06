// Contrato das entidades que pertencem a uma sessao (a "sandbox" de cada visitante).
// O AppDbContext usa esta interface para preencher e filtrar o SessaoId sozinho.
public interface IDaSessao
{
    Guid SessaoId { get; set; }
}
