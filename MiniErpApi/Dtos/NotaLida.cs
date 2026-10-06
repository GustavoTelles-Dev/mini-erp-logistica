// O que a IA conseguiu ler da imagem. Tudo pode vir nulo (campo ilegivel ou inexistente).
// Este formato e o mesmo "molde" (schema) que mandamos para o Gemini responder.
public class NotaLida
{
    public bool EhNotaFiscal { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Numero { get; set; }
    public string? Serie { get; set; }
    public string? DataEmissao { get; set; }        // AAAA-MM-DD
    public string? EmitenteCnpj { get; set; }
    public string? EmitenteNome { get; set; }
    public string? DestinatarioDocumento { get; set; }
    public string? DestinatarioNome { get; set; }
    public decimal? ValorTotal { get; set; }
    public List<ItemLido> Itens { get; set; } = new();

    // Campos que a propria IA marcou como "li com duvida" (borrado, cortado, ambiguo).
    public List<string> CamposIncertos { get; set; } = new();
}

public class ItemLido
{
    public string? Descricao { get; set; }
    public decimal? Quantidade { get; set; }
    public decimal? ValorUnitario { get; set; }
    public decimal? ValorTotal { get; set; }
}

// Um apontamento da conferencia. Bloqueia = impede salvar ate corrigir.
public record Aviso(string Campo, string Mensagem, bool Bloqueia);
