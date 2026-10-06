// Dados que o front envia para cadastrar ou editar um motorista.
public class MotoristaEntrada
{
    public string? Nome { get; set; }
    public string? Cnh { get; set; }
    public string? Telefone { get; set; }

    // A CNH chega com ou sem pontuacao; aqui sobram so os numeros.
    public string CnhSoNumeros()
    {
        return new string((Cnh ?? string.Empty).Where(char.IsDigit).ToArray());
    }

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

        if (CnhSoNumeros().Length != 11)
        {
            erros["cnh"] = new[] { "A CNH precisa ter 11 dígitos." };
        }

        if ((Telefone ?? string.Empty).Trim().Length > 20)
        {
            erros["telefone"] = new[] { "O telefone pode ter no máximo 20 caracteres." };
        }

        return erros;
    }
}
