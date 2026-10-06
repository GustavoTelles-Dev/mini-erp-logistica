using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

// Notas fiscais: leitura com IA (sem salvar), cadastro conferido, consulta e exclusao.
public static class NotaEndpoints
{
    // Tipos de arquivo aceitos (foto ou PDF da nota). Os limites de tamanho e quantidade ficam em LimitesDemo.
    private static readonly string[] TiposAceitos = { "image/jpeg", "image/png", "image/webp", "application/pdf" };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static void MapNotaEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/notas").WithTags("Notas fiscais");

        grupo.MapGet("/", Listar);
        grupo.MapGet("/{id}", Buscar);
        grupo.MapGet("/{id}/arquivo", Arquivo);
        grupo.MapPost("/interpretar", Interpretar).RequireRateLimiting("leitura-ia");
        grupo.MapPost("/", Cadastrar).RequireRateLimiting("envio-nota");
        grupo.MapDelete("/{id}", Excluir);
    }

    // Lista sem o arquivo (que e pesado); traz o cliente da entrega para exibir na tabela.
    static async Task<IResult> Listar(AppDbContext db)
    {
        var notas = await db.NotasFiscais
            .OrderByDescending(n => n.CriadaEm)
            .Select(n => new
            {
                n.Id, n.Numero, n.Serie, n.DataEmissao, n.EmitenteCnpj, n.EmitenteNome,
                n.DestinatarioNome, n.ValorTotal, n.LidaPorIa, n.CriadaEm, n.EntregaId,
                ClienteNome = n.Entrega.Cliente.Nome,
                QuantidadeItens = n.Itens.Count,
                TemArquivo = n.Arquivo != null,
            })
            .ToListAsync();

        return Results.Ok(notas);
    }

    static async Task<IResult> Buscar(int id, AppDbContext db)
    {
        var nota = await db.NotasFiscais.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null)
        {
            return Respostas.NaoEncontrado("Nota fiscal não encontrada.");
        }

        return Results.Ok(nota);
    }

    static async Task<IResult> Arquivo(int id, AppDbContext db, HttpContext contexto)
    {
        var nota = await db.NotasFiscais.FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null || nota.Arquivo == null)
        {
            return Respostas.NaoEncontrado("Arquivo não encontrado.");
        }

        // Arquivo enviado por usuario: se alguem abrir direto no navegador, nada nele pode executar
        contexto.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'; img-src 'self'; style-src 'unsafe-inline'";

        return Results.File(nota.Arquivo, nota.ArquivoTipo ?? "application/octet-stream", nota.ArquivoNome);
    }

    // Passo 1: a IA le o arquivo e a conferencia aponta o que precisa de atencao. Nada e salvo aqui.
    static async Task<IResult> Interpretar(HttpRequest requisicao, ILeitorDeNota leitor, CancellationToken cancelar)
    {
        if (!requisicao.HasFormContentType)
        {
            return Respostas.Recusado("Envie o arquivo da nota no campo 'arquivo' (multipart/form-data).");
        }

        var formulario = await requisicao.ReadFormAsync(cancelar);
        var (bytes, tipo, _, erro) = await LerArquivo(formulario.Files["arquivo"], cancelar);
        if (erro != null)
        {
            return erro;
        }

        NotaLida lida;
        try
        {
            lida = await leitor.Ler(bytes!, tipo!, cancelar);
        }
        catch (LeituraIndisponivelException falha)
        {
            return Results.Problem(title: "Leitura por IA indisponível", detail: falha.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (LeituraFalhouException falha)
        {
            return Results.Problem(title: "Não foi possível ler a nota", detail: falha.Message, statusCode: StatusCodes.Status502BadGateway);
        }

        if (!lida.EhNotaFiscal)
        {
            return Respostas.Recusado("Este arquivo não parece ser uma nota fiscal. Envie a foto ou o PDF do DANFE.");
        }

        Higienizar(lida);
        var avisos = ConferenciaNota.Conferir(ConferenciaNota.De(lida));
        return Results.Ok(new { dados = lida, avisos });
    }

    // Passo 2: o usuario conferiu os campos e confirma. Valida de novo (nunca confiar so no front) e salva.
    static async Task<IResult> Cadastrar(HttpRequest requisicao, AppDbContext db, SessaoAtual sessaoAtual, CancellationToken cancelar)
    {
        if (!requisicao.HasFormContentType)
        {
            return Respostas.Recusado("Envie os dados no campo 'dados' e o arquivo (opcional) no campo 'arquivo'.");
        }

        var formulario = await requisicao.ReadFormAsync(cancelar);

        NotaEntrada? dados;
        try
        {
            dados = JsonSerializer.Deserialize<NotaEntrada>(formulario["dados"].ToString(), Json);
        }
        catch (JsonException)
        {
            dados = null;
        }

        if (dados == null)
        {
            return Respostas.Recusado("Dados da nota inválidos.");
        }

        // Campos obrigatorios + regras fiscais que bloqueiam (CNPJ, chave de acesso...)
        var erros = dados.Validar();
        foreach (var aviso in ConferenciaNota.Conferir(ConferenciaNota.De(dados)).Where(a => a.Bloqueia))
        {
            erros.TryAdd(aviso.Campo, new[] { aviso.Mensagem });
        }

        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        bool entregaExiste = await db.Entregas.AnyAsync(e => e.Id == dados.EntregaId, cancelar);
        if (!entregaExiste)
        {
            return Respostas.Recusado("Entrega não encontrada.");
        }

        // Transacao + trava da sessao: se o visitante enviar varias notas ao mesmo tempo (race condition),
        // elas entram uma por vez, e a contagem abaixo nunca deixa passar do limite.
        await using var transacao = await db.Database.BeginTransactionAsync(cancelar);
        await GerenciadorDeSessao.Travar(db, sessaoAtual.Id, cancelar);

        int notasDaSessao = await db.NotasFiscais.CountAsync(cancelar);
        if (notasDaSessao >= LimitesDemo.NotasPorSessao)
        {
            return Respostas.Recusado($"Limite de {LimitesDemo.NotasPorSessao} notas por sessão de demonstração atingido. Exclua alguma nota ou use \"Restaurar dados\".");
        }

        var nota = new NotaFiscal
        {
            EntregaId = dados.EntregaId,
            ChaveAcesso = ConferenciaNota.SoNumeros(dados.ChaveAcesso),
            Numero = dados.Numero!.Trim(),
            Serie = (dados.Serie ?? string.Empty).Trim(),
            DataEmissao = dados.DataEmissao,
            EmitenteCnpj = ConferenciaNota.Normalizar(dados.EmitenteCnpj),
            EmitenteNome = dados.EmitenteNome!.Trim(),
            DestinatarioDocumento = ConferenciaNota.Normalizar(dados.DestinatarioDocumento),
            DestinatarioNome = (dados.DestinatarioNome ?? string.Empty).Trim(),
            ValorTotal = dados.ValorTotal,
            LidaPorIa = dados.LidaPorIa,
            CriadaEm = DateTime.UtcNow,
            Itens = dados.Itens!.Select(i => new ItemNota
            {
                Descricao = i.Descricao!.Trim(),
                Quantidade = i.Quantidade,
                ValorUnitario = i.ValorUnitario,
                ValorTotal = i.ValorTotal,
            }).ToList(),
        };

        // O arquivo e opcional (a nota pode ser digitada sem foto)
        var arquivo = formulario.Files["arquivo"];
        if (arquivo != null)
        {
            var (bytes, tipo, nome, erro) = await LerArquivo(arquivo, cancelar);
            if (erro != null)
            {
                return erro;
            }

            // Espaco total ocupado por arquivos de todas as sessoes (protege o limite do banco)
            long ocupado = await db.NotasFiscais.IgnoreQueryFilters().SumAsync(n => (long)n.ArquivoTamanho, cancelar);
            if (ocupado + bytes!.Length > LimitesDemo.EspacoTotalArquivos)
            {
                return Results.Problem(title: "Espaço esgotado",
                    detail: "O espaço de arquivos da demonstração está cheio agora. Salve a nota sem o arquivo ou tente mais tarde.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            nota.Arquivo = bytes;
            nota.ArquivoTipo = tipo;
            nota.ArquivoNome = nome;
            nota.ArquivoTamanho = bytes.Length;
        }

        db.NotasFiscais.Add(nota);
        await db.SaveChangesAsync(cancelar);
        await transacao.CommitAsync(cancelar);

        return Results.Created($"/notas/{nota.Id}", nota);
    }

    static async Task<IResult> Excluir(int id, AppDbContext db)
    {
        var nota = await db.NotasFiscais.Include(n => n.Itens).FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null)
        {
            return Respostas.NaoEncontrado("Nota fiscal não encontrada.");
        }

        db.NotasFiscais.Remove(nota);   // os itens saem junto (cascata)
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    // Le e valida o arquivo: tamanho, tipo declarado e a "assinatura" real dos bytes
    // (evita aceitar um arquivo qualquer renomeado para .jpg).
    private static async Task<(byte[]? bytes, string? tipo, string? nome, IResult? erro)> LerArquivo(IFormFile? arquivo, CancellationToken cancelar)
    {
        if (arquivo == null || arquivo.Length == 0)
        {
            return (null, null, null, Respostas.Recusado("Envie a foto ou o PDF da nota."));
        }

        if (arquivo.Length > LimitesDemo.TamanhoMaximoArquivo)
        {
            return (null, null, null, Respostas.Recusado("O arquivo pode ter no máximo 5 MB."));
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancelar);
        byte[] bytes = memoria.ToArray();

        string? tipo = DetectarTipo(bytes);
        if (tipo == null || !TiposAceitos.Contains(tipo))
        {
            return (null, null, null, Respostas.Recusado("Formato não aceito. Envie JPG, PNG, WEBP ou PDF."));
        }

        string nome = Path.GetFileName(arquivo.FileName);
        if (nome.Length > 120)
        {
            nome = nome[^120..];
        }

        return (bytes, tipo, nome, null);
    }

    // A resposta da IA e tratada como dado nao confiavel: corta textos longos demais e limita a lista de itens.
    private static void Higienizar(NotaLida lida)
    {
        lida.ChaveAcesso = Cortar(lida.ChaveAcesso, 60);
        lida.Numero = Cortar(lida.Numero, 20);
        lida.Serie = Cortar(lida.Serie, 5);
        lida.DataEmissao = Cortar(lida.DataEmissao, 10);
        lida.EmitenteCnpj = Cortar(lida.EmitenteCnpj, 20);
        lida.EmitenteNome = Cortar(lida.EmitenteNome, 150);
        lida.DestinatarioDocumento = Cortar(lida.DestinatarioDocumento, 20);
        lida.DestinatarioNome = Cortar(lida.DestinatarioNome, 150);
        // A IA pode devolver a lista nula; aqui vira lista vazia
        lida.Itens = (lida.Itens ?? new List<ItemLido>()).Take(NotaEntrada.MaximoItens).ToList();
        foreach (var item in lida.Itens)
        {
            item.Descricao = Cortar(item.Descricao, 200);
        }

        lida.CamposIncertos = (lida.CamposIncertos ?? new List<string>()).Take(20).ToList();
    }

    // Tira os espacos das pontas e corta o texto no tamanho maximo
    private static string? Cortar(string? texto, int maximo)
    {
        if (texto == null)
        {
            return null;
        }

        string limpo = texto.Trim();
        if (limpo.Length > maximo)
        {
            return limpo[..maximo];
        }

        return limpo;
    }

    // Descobre o tipo real do arquivo pelos primeiros bytes ("assinatura"), sem confiar no nome nem no tipo enviado
    private static string? DetectarTipo(byte[] bytes)
    {
        if (ComecaCom(bytes, 0xFF, 0xD8, 0xFF))
        {
            return "image/jpeg";
        }

        if (ComecaCom(bytes, 0x89, 0x50, 0x4E, 0x47))
        {
            return "image/png";
        }

        // WEBP: "RIFF" no inicio e "WEBP" a partir do byte 8
        if (ComecaCom(bytes, 0x52, 0x49, 0x46, 0x46) && bytes.Length >= 12 &&
            bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
        {
            return "image/webp";
        }

        // PDF: "%PDF"
        if (ComecaCom(bytes, 0x25, 0x50, 0x44, 0x46))
        {
            return "application/pdf";
        }

        return null;
    }

    private static bool ComecaCom(byte[] bytes, params byte[] assinatura)
    {
        if (bytes.Length < assinatura.Length)
        {
            return false;
        }

        for (int i = 0; i < assinatura.Length; i++)
        {
            if (bytes[i] != assinatura[i])
            {
                return false;
            }
        }

        return true;
    }
}
