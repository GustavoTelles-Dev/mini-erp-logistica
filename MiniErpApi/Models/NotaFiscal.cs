using System.Text.Json.Serialization;

// Nota fiscal vinculada a uma entrega (a mercadoria que a entrega leva).
// Os campos podem ser preenchidos pela IA, mas so sao salvos depois que o usuario confere.
public class NotaFiscal : IDaSessao
{
    public int Id { get; set; }

    public int EntregaId { get; set; }
    [JsonIgnore]
    public Entrega Entrega { get; set; } = null!;

    public string ChaveAcesso { get; set; } = string.Empty;   // 44 digitos (pode ficar vazia)
    public string Numero { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public DateOnly? DataEmissao { get; set; }

    public string EmitenteCnpj { get; set; } = string.Empty;
    public string EmitenteNome { get; set; } = string.Empty;
    public string DestinatarioDocumento { get; set; } = string.Empty;   // CNPJ ou CPF
    public string DestinatarioNome { get; set; } = string.Empty;

    public decimal ValorTotal { get; set; }
    public List<ItemNota> Itens { get; set; } = new();

    // true quando os campos vieram da leitura por IA (mesmo que o usuario tenha ajustado depois)
    public bool LidaPorIa { get; set; }
    public DateTime CriadaEm { get; set; }

    // Arquivo original (foto ou PDF), guardado no proprio banco. Fica fora do JSON.
    [JsonIgnore]
    public byte[]? Arquivo { get; set; }
    public string? ArquivoTipo { get; set; }
    public string? ArquivoNome { get; set; }

    [JsonIgnore]
    public Guid SessaoId { get; set; }
}
