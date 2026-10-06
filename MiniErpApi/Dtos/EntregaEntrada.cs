// Dados para cadastrar uma entrega. Status e datas nao entram aqui:
// toda entrega nasce Pendente, com a data de agora (quem decide e a API).
public class EntregaEntrada
{
    public string? Endereco { get; set; }
    public int ClienteId { get; set; }

    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Endereco) || Endereco.Trim().Length < 5)
        {
            erros["endereco"] = new[] { "Informe o endereço com rua, número e cidade." };
        }
        else if (Endereco.Trim().Length > 200)
        {
            erros["endereco"] = new[] { "O endereço pode ter no máximo 200 caracteres." };
        }

        if (ClienteId <= 0)
        {
            erros["clienteId"] = new[] { "Escolha o cliente da entrega." };
        }

        return erros;
    }
}
