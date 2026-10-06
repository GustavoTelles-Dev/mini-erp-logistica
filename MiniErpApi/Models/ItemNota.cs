using System.Text.Json.Serialization;

// Produto da nota: descricao, quantidade e valores.
public class ItemNota : IDaSessao
{
    public int Id { get; set; }
    public int NotaFiscalId { get; set; }

    public string Descricao { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }

    [JsonIgnore]
    public Guid SessaoId { get; set; }
}
