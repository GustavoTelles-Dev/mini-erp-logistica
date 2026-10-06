// Testes das regras fiscais da ConferenciaNota.
// Os documentos usados aqui sao ficticios, mas com digitos verificadores corretos
// (os mesmos da nota de exemplo que vem no sistema).
public class ConferenciaNotaTestes
{
    private const string CnpjEmitente = "11222333000181";
    private const string CnpjDestinatario = "12345678000195";
    private const string ChaveExemplo = "35261011222333000181550010000048211735192649";

    // Monta a nota de exemplo do sistema; cada teste muda so o campo que quer testar.
    private static ConferenciaNota.Dados NotaExemplo(
        string? chave = ChaveExemplo, string? numero = "000.004.821", string? serie = "1",
        DateOnly? data = null, string? emitente = CnpjEmitente, string? destinatario = CnpjDestinatario,
        decimal? total = 1120.60m)
    {
        var itens = new List<(decimal?, decimal?, decimal?)>
        {
            (50m, 6.90m, 345.00m),
            (24m, 8.50m, 204.00m),
            (4m, 79.90m, 319.60m),
            (6m, 42.00m, 252.00m),
        };

        return new ConferenciaNota.Dados(chave, numero, serie, data ?? new DateOnly(2026, 10, 3), emitente, destinatario, total, itens);
    }

    // ---------- CNPJ ----------

    [Theory]
    [InlineData("11222333000181")]
    [InlineData("12345678000195")]
    [InlineData("12ABC34501DE35")]   // exemplo oficial do CNPJ alfanumerico da Receita Federal
    public void Cnpj_valido_e_aceito(string cnpj)
    {
        Assert.True(ConferenciaNota.CnpjValido(cnpj));
    }

    [Theory]
    [InlineData("11222333000182")]   // ultimo digito trocado
    [InlineData("11111111111111")]   // todos iguais
    [InlineData("1122233300018")]    // 13 caracteres
    [InlineData("12ABC34501DEAB")]   // digitos verificadores precisam ser numeros
    public void Cnpj_invalido_e_recusado(string cnpj)
    {
        Assert.False(ConferenciaNota.CnpjValido(cnpj));
    }

    [Fact]
    public void Normalizar_tira_pontuacao_e_deixa_maiusculo()
    {
        Assert.Equal("12ABC34501DE35", ConferenciaNota.Normalizar("12.abc.345/01de-35"));
    }

    // ---------- CPF ----------

    [Fact]
    public void Cpf_valido_e_aceito()
    {
        Assert.True(ConferenciaNota.CpfValido("52998224725"));
    }

    [Theory]
    [InlineData("52998224726")]
    [InlineData("00000000000")]
    [InlineData("5299822472")]
    public void Cpf_invalido_e_recusado(string cpf)
    {
        Assert.False(ConferenciaNota.CpfValido(cpf));
    }

    // ---------- Chave de acesso ----------

    [Fact]
    public void Chave_de_acesso_com_digito_certo_e_aceita()
    {
        Assert.True(ConferenciaNota.ChaveValida(ChaveExemplo));
    }

    [Fact]
    public void Chave_de_acesso_com_digito_errado_e_recusada()
    {
        string chaveErrada = ChaveExemplo[..43] + "0";
        Assert.False(ConferenciaNota.ChaveValida(chaveErrada));
    }

    // ---------- Conferencia completa ----------

    [Fact]
    public void Nota_de_exemplo_passa_sem_nenhum_aviso()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo());
        Assert.Empty(avisos);
    }

    [Fact]
    public void Cnpj_do_emitente_errado_bloqueia_o_salvamento()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo(emitente: "11222333000182"));

        // Bloqueia pelo digito verificador (e tambem avisa que nao bate com a chave)
        Assert.Contains(avisos, a => a.Campo == "emitenteCnpj" && a.Bloqueia);
        Assert.Contains(avisos, a => a.Campo == "emitenteCnpj" && !a.Bloqueia);
    }

    [Fact]
    public void Cnpj_sem_preencher_bloqueia_o_salvamento()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo(emitente: ""));
        Assert.Contains(avisos, a => a.Campo == "emitenteCnpj" && a.Bloqueia);
    }

    [Fact]
    public void Cpf_do_destinatario_invalido_bloqueia()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo(destinatario: "529.982.247-26"));
        Assert.Contains(avisos, a => a.Campo == "destinatarioDocumento" && a.Bloqueia);
    }

    [Fact]
    public void Chave_com_tamanho_errado_bloqueia()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo(chave: "3526101122233300018155"));
        Assert.Contains(avisos, a => a.Campo == "chaveAcesso" && a.Bloqueia);
    }

    [Fact]
    public void Numero_serie_e_data_que_nao_batem_com_a_chave_so_avisam()
    {
        var avisos = ConferenciaNota.Conferir(NotaExemplo(numero: "4822", serie: "2", data: new DateOnly(2026, 9, 30)));

        Assert.Contains(avisos, a => a.Campo == "numero" && !a.Bloqueia);
        Assert.Contains(avisos, a => a.Campo == "serie" && !a.Bloqueia);
        Assert.Contains(avisos, a => a.Campo == "dataEmissao" && !a.Bloqueia);
    }

    [Fact]
    public void Soma_dos_itens_diferente_do_total_so_avisa()
    {
        // Frete ou desconto podem explicar a diferenca, por isso nao bloqueia
        var avisos = ConferenciaNota.Conferir(NotaExemplo(total: 1200m));

        var aviso = Assert.Single(avisos);
        Assert.Equal("valorTotal", aviso.Campo);
        Assert.False(aviso.Bloqueia);
    }

    [Fact]
    public void Item_com_conta_errada_e_apontado()
    {
        var nota = NotaExemplo() with
        {
            Itens = new List<(decimal?, decimal?, decimal?)> { (2m, 10m, 25m) },
            ValorTotal = 25m,
        };

        var avisos = ConferenciaNota.Conferir(nota);
        Assert.Contains(avisos, a => a.Campo == "itens" && a.Mensagem.StartsWith("Item 1"));
    }

    [Fact]
    public void Data_no_futuro_e_apontada()
    {
        var amanhaDepois = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var avisos = ConferenciaNota.Conferir(NotaExemplo(chave: null, data: amanhaDepois));

        Assert.Contains(avisos, a => a.Campo == "dataEmissao" && a.Mensagem.Contains("futuro"));
    }
}
