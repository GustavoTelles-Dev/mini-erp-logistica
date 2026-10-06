// Dados que o front envia para cadastrar ou editar um cliente.
// Separar a "entrada" da entidade impede que alguem envie campos que nao deveria (Id, SessaoId...).
public class ClienteEntrada
{
    public string? Nome { get; set; }
    public int Idade { get; set; }
    public bool Ativo { get; set; } = true;

    // Devolve os erros por campo. Dicionario vazio = dados validos.
    public Dictionary<string, string[]> Validar()
    {
        var erros = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(Nome))
        {
            erros["nome"] = new[] { "Informe o nome." };
        }
        else if (Nome.Trim().Length > 120)
        {
            erros["nome"] = new[] { "O nome pode ter no máximo 120 caracteres." };
        }

        if (Idade <= 0 || Idade > 120)
        {
            erros["idade"] = new[] { "Informe uma idade entre 1 e 120." };
        }

        return erros;
    }
}
