// Respostas de erro padronizadas no formato "Problem Details" (padrao da web, RFC 9457).
// O front sempre le o campo "detail" (regra de negocio) ou "errors" (campos invalidos).
public static class Respostas
{
    public static IResult Recusado(string mensagem)
    {
        return Results.Problem(title: "Operação não permitida", detail: mensagem, statusCode: StatusCodes.Status400BadRequest);
    }

    public static IResult NaoEncontrado(string mensagem)
    {
        return Results.Problem(title: "Não encontrado", detail: mensagem, statusCode: StatusCodes.Status404NotFound);
    }
}
