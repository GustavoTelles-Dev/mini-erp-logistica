using Microsoft.EntityFrameworkCore;

// Endpoints de entregas: listar, cadastrar, mudar o status (despacho / entregue) e excluir.
public static class EntregaEndpoints
{
    public static void MapEntregaEndpoints(this WebApplication app)
    {
        var grupo = app.MapGroup("/entregas").WithTags("Entregas");

        grupo.MapGet("/", Listar);
        grupo.MapPost("/", Cadastrar);
        grupo.MapPut("/{id}", AtualizarStatus);
        grupo.MapDelete("/{id}", Excluir);
    }

    // Include traz junto o Cliente e o Motorista de cada entrega (sem ele viriam nulos).
    static async Task<IResult> Listar(AppDbContext db)
    {
        var entregas = await db.Entregas
            .Include(e => e.Cliente)
            .Include(e => e.Motorista)
            .OrderByDescending(e => e.CriadaEm)
            .ToListAsync();

        return Results.Ok(entregas);
    }

    static async Task<IResult> Cadastrar(EntregaEntrada dados, AppDbContext db)
    {
        var erros = dados.Validar();
        if (erros.Count > 0)
        {
            return Results.ValidationProblem(erros);
        }

        var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.Id == dados.ClienteId);
        if (cliente == null)
        {
            return Respostas.Recusado("Cliente não encontrado.");
        }

        if (!cliente.Ativo)
        {
            return Respostas.Recusado("Cliente inativo não pode receber entregas. Ative o cliente primeiro.");
        }

        int cadastradas = await db.Entregas.CountAsync();
        if (cadastradas >= LimitesDemo.CadastrosPorTabela)
        {
            return Respostas.Recusado(LimitesDemo.MensagemCadastroCheio("entregas"));
        }

        // Toda entrega nasce Pendente, sem motorista, com a data de agora.
        var entrega = new Entrega
        {
            Endereco = dados.Endereco!.Trim(),
            ClienteId = cliente.Id,
            Status = StatusEntrega.Pendente,
            CriadaEm = DateTime.UtcNow,
        };

        db.Entregas.Add(entrega);
        await db.SaveChangesAsync();

        return Results.Created($"/entregas/{entrega.Id}", entrega);
    }

    // Regra de negocio do ciclo de vida, sem pular etapa:
    // Pendente -> EmTransito (despacho, exige motorista) -> Entregue.
    static async Task<IResult> AtualizarStatus(int id, StatusEntrada dados, AppDbContext db)
    {
        var entrega = await db.Entregas.FirstOrDefaultAsync(e => e.Id == id);
        if (entrega == null)
        {
            return Respostas.NaoEncontrado("Entrega não encontrada.");
        }

        if (dados.Status == null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["status"] = new[] { "Informe o novo status: EmTransito ou Entregue." },
            });
        }

        if (dados.Status == StatusEntrega.EmTransito)
        {
            if (entrega.Status != StatusEntrega.Pendente)
            {
                return Respostas.Recusado("Só uma entrega pendente pode ser despachada.");
            }

            if (dados.MotoristaId == null)
            {
                return Respostas.Recusado("Para despachar é necessário escolher um motorista.");
            }

            bool motoristaExiste = await db.Motoristas.AnyAsync(m => m.Id == dados.MotoristaId);
            if (!motoristaExiste)
            {
                return Respostas.Recusado("Motorista não encontrado.");
            }

            entrega.MotoristaId = dados.MotoristaId;
            entrega.DespachadaEm = DateTime.UtcNow;
        }
        else if (dados.Status == StatusEntrega.Entregue)
        {
            if (entrega.Status != StatusEntrega.EmTransito)
            {
                return Respostas.Recusado("Só uma entrega em trânsito pode ser marcada como entregue.");
            }

            entrega.EntregueEm = DateTime.UtcNow;
        }
        else
        {
            return Respostas.Recusado("Uma entrega não pode voltar para Pendente.");
        }

        entrega.Status = dados.Status.Value;
        await db.SaveChangesAsync();

        return Results.Ok(entrega);
    }

    // Entrega com nota fiscal vinculada nao pode sumir (a nota ficaria sem entrega).
    static async Task<IResult> Excluir(int id, AppDbContext db)
    {
        var entrega = await db.Entregas.FirstOrDefaultAsync(e => e.Id == id);
        if (entrega == null)
        {
            return Respostas.NaoEncontrado("Entrega não encontrada.");
        }

        bool temNotas = await db.NotasFiscais.AnyAsync(n => n.EntregaId == id);
        if (temNotas)
        {
            return Respostas.Recusado("Esta entrega tem nota fiscal vinculada e não pode ser excluída. Exclua a nota primeiro.");
        }

        db.Entregas.Remove(entrega);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
