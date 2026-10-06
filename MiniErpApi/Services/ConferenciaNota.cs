using System.Globalization;

// Conferencia da nota: regras fiscais escritas por nos, que checam o que a IA (ou o usuario) preencheu.
// A IA le; o codigo confere. Assim um erro de leitura nao passa despercebido.
public static class ConferenciaNota
{
    // Os dados que a conferencia precisa, vindos da leitura da IA ou do formulario.
    public record Dados(
        string? ChaveAcesso, string? Numero, string? Serie, DateOnly? DataEmissao,
        string? EmitenteCnpj, string? DestinatarioDocumento, decimal? ValorTotal,
        List<(decimal? Quantidade, decimal? ValorUnitario, decimal? ValorTotal)> Itens);

    public static Dados De(NotaLida lida)
    {
        DateOnly? data = DateOnly.TryParse(lida.DataEmissao, CultureInfo.InvariantCulture, out var d) ? d : null;
        var itens = lida.Itens.Select(i => (i.Quantidade, i.ValorUnitario, i.ValorTotal)).ToList();
        return new Dados(lida.ChaveAcesso, lida.Numero, lida.Serie, data, lida.EmitenteCnpj, lida.DestinatarioDocumento, lida.ValorTotal, itens);
    }

    public static Dados De(NotaEntrada nota)
    {
        var itens = nota.Itens.Select(i => ((decimal?)i.Quantidade, (decimal?)i.ValorUnitario, (decimal?)i.ValorTotal)).ToList();
        return new Dados(nota.ChaveAcesso, nota.Numero, nota.Serie, nota.DataEmissao, nota.EmitenteCnpj, nota.DestinatarioDocumento, nota.ValorTotal, itens);
    }

    public static List<Aviso> Conferir(Dados nota)
    {
        var avisos = new List<Aviso>();

        // 1) CNPJ do emitente: digitos verificadores (aceita o CNPJ alfanumerico)
        string cnpj = Normalizar(nota.EmitenteCnpj);
        if (cnpj.Length == 0)
        {
            avisos.Add(new Aviso("emitenteCnpj", "Informe o CNPJ do emitente.", true));
        }
        else if (!CnpjValido(cnpj))
        {
            avisos.Add(new Aviso("emitenteCnpj", "CNPJ do emitente inválido (os dígitos verificadores não conferem).", true));
        }

        // 2) Documento do destinatario (CPF ou CNPJ), se informado
        string dest = Normalizar(nota.DestinatarioDocumento);
        if (dest.Length > 0 && !(dest.Length == 11 ? CpfValido(dest) : CnpjValido(dest)))
        {
            avisos.Add(new Aviso("destinatarioDocumento", "CPF/CNPJ do destinatário inválido.", true));
        }

        // 3) Chave de acesso: 44 digitos, digito verificador e cruzamento com os outros campos
        string chave = SoNumeros(nota.ChaveAcesso);
        if (chave.Length > 0)
        {
            if (chave.Length != 44)
            {
                avisos.Add(new Aviso("chaveAcesso", $"A chave de acesso tem 44 dígitos (foram lidos {chave.Length}).", true));
            }
            else if (!ChaveValida(chave))
            {
                avisos.Add(new Aviso("chaveAcesso", "Chave de acesso inválida (o dígito verificador não confere).", true));
            }
            else
            {
                // A chave "carrega" dados da nota: AAMM da emissao, CNPJ, serie e numero.
                if (cnpj.Length == 14 && chave.Substring(6, 14) != cnpj)
                {
                    avisos.Add(new Aviso("emitenteCnpj", "O CNPJ do emitente não bate com o que está na chave de acesso.", false));
                }

                if (int.TryParse(nota.Serie, out int serie) && int.Parse(chave.Substring(22, 3)) != serie)
                {
                    avisos.Add(new Aviso("serie", "A série não bate com a chave de acesso.", false));
                }

                if (long.TryParse(SoNumeros(nota.Numero), out long numero) && long.Parse(chave.Substring(25, 9)) != numero)
                {
                    avisos.Add(new Aviso("numero", "O número da nota não bate com a chave de acesso.", false));
                }

                if (nota.DataEmissao != null && chave.Substring(2, 4) != nota.DataEmissao.Value.ToString("yyMM"))
                {
                    avisos.Add(new Aviso("dataEmissao", "O mês/ano de emissão não bate com a chave de acesso.", false));
                }
            }
        }

        // 4) Data de emissao no futuro
        if (nota.DataEmissao != null && nota.DataEmissao.Value > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
        {
            avisos.Add(new Aviso("dataEmissao", "A data de emissão está no futuro.", false));
        }

        // 5) Itens: quantidade x valor unitario = total do item
        for (int i = 0; i < nota.Itens.Count; i++)
        {
            var item = nota.Itens[i];
            if (item.Quantidade != null && item.ValorUnitario != null && item.ValorTotal != null &&
                Math.Abs(item.Quantidade.Value * item.ValorUnitario.Value - item.ValorTotal.Value) > 0.05m)
            {
                avisos.Add(new Aviso("itens", $"Item {i + 1}: quantidade × valor unitário não dá o total do item.", false));
            }
        }

        // 6) Soma dos itens x total da nota (pode diferir por frete, desconto ou impostos: so avisa)
        decimal somaItens = nota.Itens.Sum(i => i.ValorTotal ?? 0);
        if (nota.Itens.Count > 0 && nota.ValorTotal != null && Math.Abs(somaItens - nota.ValorTotal.Value) > 0.05m)
        {
            avisos.Add(new Aviso("valorTotal",
                $"A soma dos itens ({somaItens.ToString("C", new CultureInfo("pt-BR"))}) é diferente do total da nota. Confira frete, descontos ou impostos.", false));
        }

        return avisos;
    }

    // ---------- regras de calculo ----------

    public static string SoNumeros(string? texto)
    {
        return new string((texto ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    // Remove pontuacao e deixa maiusculo (o CNPJ alfanumerico tem letras)
    public static string Normalizar(string? texto)
    {
        return new string((texto ?? string.Empty).ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    // CNPJ (numerico ou alfanumerico): cada caractere vale (codigo ASCII - 48), pesos 2..9 da direita para a esquerda.
    public static bool CnpjValido(string cnpj)
    {
        if (cnpj.Length != 14 || !char.IsDigit(cnpj[12]) || !char.IsDigit(cnpj[13]))
        {
            return false;
        }

        if (cnpj.Distinct().Count() == 1)
        {
            return false;   // 00000000000000, 11111111111111...
        }

        int Digito(string baseCnpj)
        {
            int soma = 0, peso = 2;
            for (int i = baseCnpj.Length - 1; i >= 0; i--)
            {
                soma += (baseCnpj[i] - 48) * peso;
                peso = peso == 9 ? 2 : peso + 1;
            }
            int resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        int dv1 = Digito(cnpj.Substring(0, 12));
        int dv2 = Digito(cnpj.Substring(0, 12) + dv1);
        return cnpj[12] - '0' == dv1 && cnpj[13] - '0' == dv2;
    }

    public static bool CpfValido(string cpf)
    {
        if (cpf.Length != 11 || !cpf.All(char.IsDigit) || cpf.Distinct().Count() == 1)
        {
            return false;
        }

        for (int t = 9; t < 11; t++)
        {
            int soma = 0;
            for (int i = 0; i < t; i++)
            {
                soma += (cpf[i] - '0') * (t + 1 - i);
            }
            int dv = soma * 10 % 11 % 10;
            if (cpf[t] - '0' != dv)
            {
                return false;
            }
        }

        return true;
    }

    // Chave de acesso NF-e: modulo 11 com pesos 2..9 sobre os 43 primeiros digitos.
    public static bool ChaveValida(string chave)
    {
        int soma = 0, peso = 2;
        for (int i = 42; i >= 0; i--)
        {
            soma += (chave[i] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }
        int resto = soma % 11;
        int dv = resto < 2 ? 0 : 11 - resto;
        return chave[43] - '0' == dv;
    }
}
