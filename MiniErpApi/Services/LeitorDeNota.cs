using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using TipoSchema = Google.GenAI.Types.Type;

// Contrato do "leitor" de notas. O endpoint depende so desta interface;
// trocar o Gemini por outra IA no futuro nao mexe no resto do sistema.
public interface ILeitorDeNota
{
    Task<NotaLida> Ler(byte[] arquivo, string tipo, CancellationToken cancelar);
}

public class LeituraIndisponivelException : Exception
{
    public LeituraIndisponivelException(string mensagem) : base(mensagem) { }
}

public class LeituraFalhouException : Exception
{
    public LeituraFalhouException(string mensagem, Exception? interna = null) : base(mensagem, interna) { }
}

// Implementacao com o Gemini: manda a imagem + instrucoes e exige a resposta num formato fixo (schema).
public class LeitorDeNotaGemini : ILeitorDeNota
{
    private readonly IConfiguration _config;
    private readonly ILogger<LeitorDeNotaGemini> _log;

    public LeitorDeNotaGemini(IConfiguration config, ILogger<LeitorDeNotaGemini> log)
    {
        _config = config;
        _log = log;
    }

    private const string Instrucoes = """
        Você lê documentos fiscais brasileiros (DANFE de NF-e, NFC-e ou cupom fiscal) a partir de uma imagem ou PDF.
        Regras:
        - Copie exatamente o que está escrito. Nunca invente nem complete dados.
        - Se um campo não existir ou não estiver legível, devolva null.
        - chaveAcesso: os 44 dígitos, só números.
        - emitenteCnpj e destinatarioDocumento: só números (ou letras e números, no CNPJ alfanumérico).
        - dataEmissao: formato AAAA-MM-DD.
        - Valores: número com ponto decimal, sem "R$" (ex.: 1234.56).
        - itens: um por produto, com descrição, quantidade, valor unitário e valor total do item.
        - camposIncertos: nomes dos campos que você leu com dúvida (borrado, cortado ou ambíguo).
        - Se o documento não for uma nota fiscal, devolva ehNotaFiscal = false e o resto nulo.
        """;

    public async Task<NotaLida> Ler(byte[] arquivo, string tipo, CancellationToken cancelar)
    {
        string? chave = _config["Gemini:ChaveApi"];
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new LeituraIndisponivelException("A leitura por IA não está configurada neste servidor. Preencha os campos manualmente.");
        }

        // Modelo principal e reserva: se o principal estiver sobrecarregado (acontece em horario de pico),
        // a leitura tenta de novo no modelo mais leve antes de desistir.
        string[] modelos =
        {
            _config["Gemini:Modelo"] ?? "gemini-flash-latest",
            _config["Gemini:ModeloReserva"] ?? "gemini-flash-lite-latest",
        };
        var cliente = new Client(apiKey: chave);

        var conteudo = new List<Content>
        {
            new Content
            {
                Role = "user",
                Parts = new List<Part>
                {
                    new Part { Text = Instrucoes },
                    new Part { InlineData = new Blob { MimeType = tipo, Data = arquivo } },
                },
            },
        };

        var configuracao = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = Molde(),
            Temperature = 0,   // leitura, nao criatividade
            // Transcrever um documento nao exige "raciocinio" longo: nivel baixo deixa a leitura bem mais rapida.
            ThinkingConfig = new ThinkingConfig { ThinkingLevel = ThinkingLevel.Low },
        };

        string? texto = null;
        Exception? ultimoErro = null;

        foreach (string modelo in modelos)
        {
            try
            {
                var resposta = await cliente.Models.GenerateContentAsync(model: modelo, contents: conteudo, config: configuracao);
                texto = resposta.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
                break;
            }
            catch (Exception erro) when (!cancelar.IsCancellationRequested)
            {
                ultimoErro = erro;
                _log.LogWarning(erro, "Falha ao ler a nota com o modelo {Modelo}. Tentando o proximo.", modelo);
            }
        }

        if (texto == null && ultimoErro != null)
        {
            throw new LeituraFalhouException("O serviço de IA está sobrecarregado agora. Tente de novo em instantes ou preencha manualmente.", ultimoErro);
        }

        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new LeituraFalhouException("A IA não conseguiu ler este arquivo. Tente uma foto mais nítida ou preencha manualmente.");
        }

        try
        {
            var lida = JsonSerializer.Deserialize<NotaLida>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return lida ?? throw new LeituraFalhouException("Resposta vazia da IA.");
        }
        catch (JsonException erro)
        {
            throw new LeituraFalhouException("A IA respondeu num formato inesperado. Tente de novo ou preencha manualmente.", erro);
        }
    }

    // O "molde" da resposta: o Gemini e obrigado a devolver exatamente estes campos.
    private static Schema Molde()
    {
        Schema Texto(string descricao) => new Schema { Type = TipoSchema.String, Nullable = true, Description = descricao };
        Schema Numero(string descricao) => new Schema { Type = TipoSchema.Number, Nullable = true, Description = descricao };

        var campos = new List<string>
        {
            "chaveAcesso", "numero", "serie", "dataEmissao", "emitenteCnpj", "emitenteNome",
            "destinatarioDocumento", "destinatarioNome", "valorTotal", "itens",
        };

        return new Schema
        {
            Type = TipoSchema.Object,
            Properties = new Dictionary<string, Schema>
            {
                ["ehNotaFiscal"] = new Schema { Type = TipoSchema.Boolean },
                ["chaveAcesso"] = Texto("Chave de acesso, 44 dígitos"),
                ["numero"] = Texto("Número da nota"),
                ["serie"] = Texto("Série da nota"),
                ["dataEmissao"] = Texto("Data de emissão no formato AAAA-MM-DD"),
                ["emitenteCnpj"] = Texto("CNPJ do emitente"),
                ["emitenteNome"] = Texto("Razão social do emitente"),
                ["destinatarioDocumento"] = Texto("CNPJ ou CPF do destinatário"),
                ["destinatarioNome"] = Texto("Nome ou razão social do destinatário"),
                ["valorTotal"] = Numero("Valor total da nota"),
                ["itens"] = new Schema
                {
                    Type = TipoSchema.Array,
                    Items = new Schema
                    {
                        Type = TipoSchema.Object,
                        Properties = new Dictionary<string, Schema>
                        {
                            ["descricao"] = Texto("Descrição do produto"),
                            ["quantidade"] = Numero("Quantidade"),
                            ["valorUnitario"] = Numero("Valor unitário"),
                            ["valorTotal"] = Numero("Valor total do item"),
                        },
                        Required = new List<string> { "descricao", "quantidade", "valorUnitario", "valorTotal" },
                    },
                },
                ["camposIncertos"] = new Schema
                {
                    Type = TipoSchema.Array,
                    Items = new Schema { Type = TipoSchema.String, Enum = campos },
                },
            },
            Required = new List<string>
            {
                "ehNotaFiscal", "chaveAcesso", "numero", "serie", "dataEmissao", "emitenteCnpj", "emitenteNome",
                "destinatarioDocumento", "destinatarioNome", "valorTotal", "itens", "camposIncertos",
            },
        };
    }
}
