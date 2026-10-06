using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Tradutor central de erros: transforma excecoes conhecidas em respostas claras (Problem Details)
// e qualquer outra em um 500 generico, sem expor detalhes internos do servidor.
public class TratadorDeErros : IExceptionHandler
{
    private readonly ILogger<TratadorDeErros> _log;

    public TratadorDeErros(ILogger<TratadorDeErros> log)
    {
        _log = log;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception erro, CancellationToken cancelar)
    {
        var (status, titulo, mensagem) = Classificar(erro);

        if (status == StatusCodes.Status500InternalServerError)
        {
            _log.LogError(erro, "Erro inesperado em {Metodo} {Rota}", contexto.Request.Method, contexto.Request.Path);
        }

        if (status == StatusCodes.Status503ServiceUnavailable)
        {
            _log.LogWarning("Banco indisponivel em {Metodo} {Rota}: {Mensagem}", contexto.Request.Method, contexto.Request.Path, erro.Message);
        }

        // Cliente desistiu da requisicao (fechou a aba): nao ha para quem responder
        if (status == 499)
        {
            return true;
        }

        await Results.Problem(title: titulo, detail: mensagem, statusCode: status).ExecuteAsync(contexto);
        return true;
    }

    private static (int status, string titulo, string mensagem) Classificar(Exception erro)
    {
        // Duas acoes alteraram o mesmo registro ao mesmo tempo (ex.: despachar a mesma entrega em duas abas)
        if (erro is DbUpdateConcurrencyException)
        {
            return (409, "Conflito", "Este registro foi alterado em outra aba ou por outra ação. Atualize a tela e tente de novo.");
        }

        // Regras do proprio banco: chave estrangeira (registro em uso) ou duplicidade
        if (erro is DbUpdateException { InnerException: PostgresException pg })
        {
            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return (409, "Conflito", "Este registro está em uso por outro cadastro e não pode ser alterado assim. Atualize a tela.");
            }

            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return (409, "Conflito", "Já existe um registro com esses dados.");
            }
        }

        // Corpo grande demais (limite do servidor ou do formulario)
        if (erro is BadHttpRequestException { StatusCode: 413 } || erro is InvalidDataException)
        {
            return (413, "Arquivo grande demais", "O envio passou do limite de 5 MB. Use um arquivo menor.");
        }

        // JSON invalido, campo com tipo errado, etc.
        if (erro is BadHttpRequestException)
        {
            return (400, "Dados inválidos", "Os dados enviados estão em um formato inválido. Confira os campos e tente de novo.");
        }

        if (erro is OperationCanceledException)
        {
            return (499, string.Empty, string.Empty);
        }

        // Banco fora do ar ou sem resposta (queda de rede, manutencao do Supabase...)
        if (BancoIndisponivel(erro))
        {
            return (503, "Banco de dados indisponível", "Não foi possível acessar o banco de dados agora. Tente de novo em alguns instantes.");
        }

        return (500, "Erro interno", "Algo deu errado do nosso lado. Tente de novo em instantes.");
    }

    // Procura, na excecao e nas internas, um erro de conexao com o Postgres.
    // PostgresException e erro de regra do banco (SQL); NpgsqlException "pura" ou timeout e falha de conexao.
    private static bool BancoIndisponivel(Exception erro)
    {
        Exception? atual = erro;
        while (atual != null)
        {
            if (atual is NpgsqlException && atual is not PostgresException)
            {
                return true;
            }

            if (atual is TimeoutException)
            {
                return true;
            }

            // Codigos do Postgres: 08 = conexao, 53 = recursos esgotados (ex.: conexoes demais), 57P = servidor desligando
            if (atual is PostgresException pg &&
                (pg.SqlState.StartsWith("08") || pg.SqlState.StartsWith("53") || pg.SqlState.StartsWith("57P")))
            {
                return true;
            }

            atual = atual.InnerException;
        }

        return false;
    }
}
