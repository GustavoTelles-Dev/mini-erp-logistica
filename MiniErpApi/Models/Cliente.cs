using System.Text.Json.Serialization;

// Cliente: quem solicita as entregas.
public class Cliente : IDaSessao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int Idade { get; set; }
    public bool Ativo { get; set; }

    // Sandbox: a sessao (visitante) dona deste registro. Fica fora do JSON.
    [JsonIgnore]
    public Guid SessaoId { get; set; }
}
