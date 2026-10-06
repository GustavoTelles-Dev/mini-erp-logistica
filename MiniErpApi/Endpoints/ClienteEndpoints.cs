using Microsoft.EntityFrameworkCore;

// Endpoints de clientes. O Program.cs so chama app.MapClienteEndpoints();
// cada operacao fica num metodo com nome proprio, facil de achar e de testar.
public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/clientes").WithTags("Clientes");

        grupo.MapGet("/", Listar);
        grupo.MapPost("/", Cadastrar);
        grupo.MapPut("/{id}", Atualizar);
        grupo.MapDelete("/{id}", Excluir);
    }

    static async Task<IResult> Listar(AppDbContext db)
    {
        var clientes = await db.Clientes.OrderBy(c => c.Nome).ToListAsync();
        return Results.Ok(clientes);
    }

    static async Task<IResult> Cadastrar(ClienteEntrada dados, AppDbContext db)
    {
        var erros = dados.Validar();
        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        var cliente = new Cliente
        {
            Nome = dados.Nome!.Trim(),
            Idade = dados.Idade,
            Ativo = dados.Ativo,
        };

        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        return Results.Created($"/clientes/{cliente.Id}", cliente);
    }

    static async Task<IResult> Atualizar(int id, ClienteEntrada dados, AppDbContext db)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id);
        if (cliente == null)
        {
            return Respostas.NaoEncontrado("Cliente não encontrado.");
        }

        var erros = dados.Validar();
        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        cliente.Nome = dados.Nome!.Trim();
        cliente.Idade = dados.Idade;
        cliente.Ativo = dados.Ativo;
        await db.SaveChangesAsync();

        return Results.Ok(cliente);
    }

    static async Task<IResult> Excluir(int id, AppDbContext db)
    {
        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == id);
        if (cliente == null)
        {
            return Respostas.NaoEncontrado("Cliente não encontrado.");
        }

        // Protecao de integridade: cliente com entregas nao pode sumir.
        bool temEntregas = await db.Entregas.AnyAsync(e => e.ClienteId == id);
        if (temEntregas)
        {
            return Respostas.Recusado("Este cliente tem entregas vinculadas e não pode ser excluído. Exclua as entregas dele primeiro.");
        }

        db.Clientes.Remove(cliente);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
