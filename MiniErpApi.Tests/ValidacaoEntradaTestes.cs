// Testes da validacao dos dados de entrada (DTOs): o que a API aceita e o que recusa.
public class ValidacaoEntradaTestes
{
    // ---------- Cliente ----------

    [Fact]
    public void Cliente_valido_nao_tem_erros()
    {
        var cliente = new ClienteEntrada { Nome = "Maria Silva", Idade = 34 };
        Assert.Empty(cliente.Validar());
    }

    [Theory]
    [InlineData(null, 30, "nome")]
    [InlineData("   ", 30, "nome")]
    [InlineData("Maria", 0, "idade")]
    [InlineData("Maria", 121, "idade")]
    public void Cliente_com_campo_invalido_aponta_o_campo(string? nome, int idade, string campo)
    {
        var erros = new ClienteEntrada { Nome = nome, Idade = idade }.Validar();
        Assert.True(erros.ContainsKey(campo));
    }

    [Fact]
    public void Cliente_com_nome_longo_demais_e_recusado()
    {
        var erros = new ClienteEntrada { Nome = new string('a', 121), Idade = 30 }.Validar();
        Assert.True(erros.ContainsKey("nome"));
    }

    // ---------- Motorista ----------

    [Fact]
    public void Motorista_aceita_cnh_com_pontuacao()
    {
        var motorista = new MotoristaEntrada { Nome = "João Souza", Cnh = "123.456.789-00" };

        Assert.Empty(motorista.Validar());
        Assert.Equal("12345678900", motorista.CnhSoNumeros());
    }

    [Theory]
    [InlineData("1234567890")]     // 10 digitos
    [InlineData("123456789012")]   // 12 digitos
    [InlineData(null)]
    public void Motorista_com_cnh_sem_11_digitos_e_recusado(string? cnh)
    {
        var erros = new MotoristaEntrada { Nome = "João", Cnh = cnh }.Validar();
        Assert.True(erros.ContainsKey("cnh"));
    }

    // ---------- Entrega ----------

    [Fact]
    public void Entrega_sem_endereco_e_sem_cliente_aponta_os_dois_campos()
    {
        var erros = new EntregaEntrada { Endereco = "Rua", ClienteId = 0 }.Validar();

        Assert.True(erros.ContainsKey("endereco"));
        Assert.True(erros.ContainsKey("clienteId"));
    }

    // ---------- Nota fiscal ----------

    private static NotaEntrada NotaValida()
    {
        return new NotaEntrada
        {
            EntregaId = 1,
            Numero = "4821",
            EmitenteNome = "Embalagens Modelo Ltda",
            EmitenteCnpj = "11222333000181",
            ValorTotal = 100m,
            Itens = new List<ItemEntrada> { new ItemEntrada { Descricao = "Caixa", Quantidade = 10, ValorUnitario = 10, ValorTotal = 100 } },
        };
    }

    [Fact]
    public void Nota_valida_nao_tem_erros()
    {
        Assert.Empty(NotaValida().Validar());
    }

    [Fact]
    public void Nota_sem_lista_de_itens_nao_quebra()
    {
        // JSON com "itens": null nao pode derrubar a API
        var nota = NotaValida();
        nota.Itens = null;

        Assert.Empty(nota.Validar());
    }

    [Fact]
    public void Nota_com_itens_demais_e_recusada()
    {
        var nota = NotaValida();
        nota.Itens = Enumerable.Range(1, NotaEntrada.MaximoItens + 1)
            .Select(i => new ItemEntrada { Descricao = "Item " + i, Quantidade = 1 })
            .ToList();

        Assert.True(nota.Validar().ContainsKey("itens"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100_000_000_000)]
    public void Nota_com_valor_total_fora_do_limite_e_recusada(decimal valor)
    {
        var nota = NotaValida();
        nota.ValorTotal = valor;

        Assert.True(nota.Validar().ContainsKey("valorTotal"));
    }

    [Fact]
    public void Nota_com_numero_longo_demais_e_recusada()
    {
        var nota = NotaValida();
        nota.Numero = new string('9', 30);

        Assert.True(nota.Validar().ContainsKey("numero"));
    }

    [Fact]
    public void Item_sem_descricao_e_recusado()
    {
        var nota = NotaValida();
        nota.Itens![0].Descricao = " ";

        Assert.True(nota.Validar().ContainsKey("itens"));
    }
}
