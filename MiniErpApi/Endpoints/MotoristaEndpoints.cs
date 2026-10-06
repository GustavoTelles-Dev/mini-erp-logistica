using Microsoft.EntityFrameworkCore;

// Endpoints de motoristas: listar, buscar, cadastrar, editar e excluir.
public static class MotoristaEndpoints
{
    public static void MapMotoristaEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/motoristas").WithTags("Motoristas");

        grupo.MapGet("/", Listar);
        grupo.MapGet("/{id}", Buscar);
        grupo.MapPost("/", Cadastrar);
        grupo.MapPut("/{id}", Atualizar);
        grupo.MapDelete("/{id}", Excluir);
    }

    static async Task<IResult> Listar(AppDbContext db)
    {
        var motoristas = await db.Motoristas.OrderBy(m => m.Nome).ToListAsync();
        return Results.Ok(motoristas);
    }

    static async Task<IResult> Buscar(int id, AppDbContext db)
    {
        var motorista = await db.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
        if (motorista == null)
        {
            return Respostas.NaoEncontrado("Motorista não encontrado.");
        }

        return Results.Ok(motorista);
    }

    static async Task<IResult> Cadastrar(MotoristaEntrada dados, AppDbContext db)
    {
        var erros = dados.Validar();
        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        int cadastrados = await db.Motoristas.CountAsync();
        if (cadastrados >= LimitesDemo.CadastrosPorTabela)
        {
            return Respostas.Recusado(LimitesDemo.MensagemCadastroCheio("motoristas"));
        }

        var motorista = new Motorista
        {
            Nome = dados.Nome!.Trim(),
            Cnh = dados.CnhSoNumeros(),
            Telefone = (dados.Telefone ?? string.Empty).Trim(),
        };

        db.Motoristas.Add(motorista);
        await db.SaveChangesAsync();

        return Results.Created($"/motoristas/{motorista.Id}", motorista);
    }

    static async Task<IResult> Atualizar(int id, MotoristaEntrada dados, AppDbContext db)
    {
        var motorista = await db.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
        if (motorista == null)
        {
            return Respostas.NaoEncontrado("Motorista não encontrado.");
        }

        var erros = dados.Validar();
        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        motorista.Nome = dados.Nome!.Trim();
        motorista.Cnh = dados.CnhSoNumeros();
        motorista.Telefone = (dados.Telefone ?? string.Empty).Trim();
        await db.SaveChangesAsync();

        return Results.Ok(motorista);
    }

    static async Task<IResult> Excluir(int id, AppDbContext db)
    {
        var motorista = await db.Motoristas.FirstOrDefaultAsync(m => m.Id == id);
        if (motorista == null)
        {
            return Respostas.NaoEncontrado("Motorista não encontrado.");
        }

        // Protecao de integridade: motorista com entregas nao pode sumir.
        bool temEntregas = await db.Entregas.AnyAsync(e => e.MotoristaId == id);
        if (temEntregas)
        {
            return Respostas.Recusado("Este motorista tem entregas vinculadas e não pode ser excluído. Exclua as entregas dele primeiro.");
        }

        db.Motoristas.Remove(motorista);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
