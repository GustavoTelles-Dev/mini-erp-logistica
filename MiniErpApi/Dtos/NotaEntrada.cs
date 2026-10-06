// Dados da nota que o front envia para salvar (depois da conferencia do usuario).
public class NotaEntrada
{
    public int EntregaId { get; set; }
    public string? ChaveAcesso { get; set; }
    public string? Numero { get; set; }
    public string? Serie { get; set; }
    public DateOnly? DataEmissao { get; set; }
    public string? EmitenteCnpj { get; set; }
    public string? EmitenteNome { get; set; }
    public string? DestinatarioDocumento { get; set; }
    public string? DestinatarioNome { get; set; }
    public decimal ValorTotal { get; set; }
    public List<ItemEntrada> Itens { get; set; } = new();
    public bool LidaPorIa { get; set; }

    // Campos obrigatorios e formatos basicos. As regras fiscais (CNPJ, chave...) ficam na ConferenciaNota.
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();

        if (EntregaId <= 0)
        {
            erros["entregaId"] = new[] { "Escolha a entrega desta nota." };
        }

        if (string.IsNullOrWhiteSpace(Numero))
        {
            erros["numero"] = new[] { "Informe o número da nota." };
        }

        if (string.IsNullOrWhiteSpace(EmitenteNome))
        {
            erros["emitenteNome"] = new[] { "Informe o nome do emitente." };
        }

        if (ValorTotal <= 0)
        {
            erros["valorTotal"] = new[] { "Informe o valor total da nota." };
        }

        for (int i = 0; i < Itens.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Itens[i].Descricao) || Itens[i].Quantidade <= 0)
            {
                erros["itens"] = new[] { $"Confira o item {i + 1}: descrição e quantidade são obrigatórias." };
                break;
            }
        }

        return erros;
    }
}

public class ItemEntrada
{
    public string? Descricao { get; set; }
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
}
