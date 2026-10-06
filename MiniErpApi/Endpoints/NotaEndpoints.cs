using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

// Notas fiscais: leitura com IA (sem salvar), cadastro conferido, consulta e exclusao.
public static class NotaEndpoints
{
    // Tipos aceitos e tamanho maximo do arquivo (foto ou PDF da nota)
    private static readonly string[] TiposAceitos = { "image/jpeg", "image/png", "image/webp", "application/pdf" };
    private const long TamanhoMaximo = 5 * 1024 * 1024;

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
        grupo.MapPost("/", Cadastrar);
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

    static async Task<IResult> Arquivo(int id, AppDbContext db)
    {
        var nota = await db.NotasFiscais.FirstOrDefaultAsync(n => n.Id == id);
        if (nota == null || nota.Arquivo == null)
        {
            return Respostas.NaoEncontrado("Arquivo não encontrado.");
        }

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
        catch (LeituraIndisponivelException ex)
        {
            return Results.Problem(title: "Leitura por IA indisponível", detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (LeituraFalhouException ex)
        {
            return Results.Problem(title: "Não foi possível ler a nota", detail: ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }

        if (!lida.EhNotaFiscal)
        {
            return Respostas.Recusado("Este arquivo não parece ser uma nota fiscal. Envie a foto ou o PDF do DANFE.");
        }

        var avisos = ConferenciaNota.Conferir(ConferenciaNota.De(lida));
        return Results.Ok(new { dados = lida, avisos });
    }

    // Passo 2: o usuario conferiu os campos e confirma. Valida de novo (nunca confiar so no front) e salva.
    static async Task<IResult> Cadastrar(HttpRequest requisicao, AppDbContext db, CancellationToken cancelar)
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
            Itens = dados.Itens.Select(i => new ItemNota
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

            nota.Arquivo = bytes;
            nota.ArquivoTipo = tipo;
            nota.ArquivoNome = nome;
        }

        db.NotasFiscais.Add(nota);
        await db.SaveChangesAsync(cancelar);

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

        if (arquivo.Length > TamanhoMaximo)
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

    private static string? DetectarTipo(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50) return "image/webp";
        if (b.Length >= 4 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46) return "application/pdf";
        return null;
    }
}
