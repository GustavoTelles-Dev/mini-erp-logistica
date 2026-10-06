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
    public List<ItemEntrada>? Itens { get; set; } = new();
    public bool LidaPorIa { get; set; }

    // Limites alinhados com as colunas do banco
    public const int MaximoItens = 100;
    public const decimal ValorMaximo = 99_999_999_999m;

    // Campos obrigatorios e formatos basicos. As regras fiscais (CNPJ, chave...) ficam na ConferenciaNota.
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();
        Itens ??= new List<ItemEntrada>();

        if (EntregaId <= 0)
        {
            erros["entregaId"] = new[] { "Escolha a entrega desta nota." };
        }

        if (string.IsNullOrWhiteSpace(Numero))
        {
            erros["numero"] = new[] { "Informe o número da nota." };
        }
        else if (Numero.Trim().Length > 20)
        {
            erros["numero"] = new[] { "O número pode ter no máximo 20 caracteres." };
        }

        if ((Serie ?? string.Empty).Trim().Length > 5)
        {
            erros["serie"] = new[] { "A série pode ter no máximo 5 caracteres." };
        }

        if ((DestinatarioNome ?? string.Empty).Trim().Length > 150)
        {
            erros["destinatarioNome"] = new[] { "O nome do destinatário pode ter no máximo 150 caracteres." };
        }

        if (string.IsNullOrWhiteSpace(EmitenteNome))
        {
            erros["emitenteNome"] = new[] { "Informe o nome do emitente." };
        }
        else if (EmitenteNome.Trim().Length > 150)
        {
            erros["emitenteNome"] = new[] { "O nome do emitente pode ter no máximo 150 caracteres." };
        }

        if (ValorTotal <= 0 || ValorTotal > ValorMaximo)
        {
            erros["valorTotal"] = new[] { "Informe um valor total válido para a nota." };
        }

        if (Itens.Count > MaximoItens)
        {
            erros["itens"] = new[] { $"A nota pode ter no máximo {MaximoItens} itens." };
            return erros;
        }

        for (int i = 0; i < Itens.Count; i++)
        {
            var item = Itens[i];
            if (string.IsNullOrWhiteSpace(item.Descricao) || item.Quantidade <= 0)
            {
                erros["itens"] = new[] { $"Confira o item {i + 1}: descrição e quantidade são obrigatórias." };
                break;
            }

            if (item.Descricao.Trim().Length > 200 || item.Quantidade > 99_999_999m ||
                item.ValorUnitario < 0 || item.ValorUnitario > ValorMaximo || item.ValorTotal < 0 || item.ValorTotal > ValorMaximo)
            {
                erros["itens"] = new[] { $"Confira o item {i + 1}: algum valor está fora do permitido." };
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
