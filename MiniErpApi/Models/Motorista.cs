using System.Text.Json.Serialization;

// Motorista: quem leva a entrega. E vinculado a entrega no despacho.
public class Motorista : IDaSessao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cnh { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;

    // Lado "muitos" do relacionamento. Fica fora do JSON para nao gerar ciclo
    // (Entrega -> Motorista -> Entregas -> Motorista...).
    [JsonIgnore]
    public List<Entrega> Entregas { get; set; } = new();

    [JsonIgnore]
    public Guid SessaoId { get; set; }
}
