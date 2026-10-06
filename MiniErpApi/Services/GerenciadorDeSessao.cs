using Microsoft.EntityFrameworkCore;

// Regras da sandbox: ler, ativar, criar, reiniciar e apagar a sessao de um visitante.
public class GerenciadorDeSessao
{
    private readonly AppDbContext _db;
    private readonly SessaoAtual _sessaoAtual;

    public GerenciadorDeSessao(AppDbContext db, SessaoAtual sessaoAtual)
    {
        _db = db;
        _sessaoAtual = sessaoAtual;
    }

    // Le o Id enviado pelo front (header X-Sessao) ou, na falta dele, pelo cookie (usado no Swagger).
    public static Guid? LerId(HttpContext contexto)
    {
        string? valor = contexto.Request.Headers["X-Sessao"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(valor))
        {
            valor = contexto.Request.Cookies["rota_sessao"];
        }

        if (Guid.TryParse(valor, out Guid id))
        {
            return id;
        }

        return null;
    }

    // Procura a sessao; se existir, registra o acesso e a torna a sessao desta requisicao.
    public async Task<Sessao?> Ativar(Guid id)
    {
        var sessao = await _db.Sessoes.FindAsync(id);

        if (sessao == null)
        {
            return null;
        }

        // So grava o "ultimo acesso" a cada 5 min, para nao escrever no banco a cada clique.
        if (DateTime.UtcNow - sessao.UltimoAcesso > TimeSpan.FromMinutes(5))
        {
            sessao.UltimoAcesso = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        _sessaoAtual.Id = sessao.Id;
        return sessao;
    }

    // Cria uma sessao nova ja com os dados de exemplo.
    public async Task<Sessao> Criar()
    {
        var sessao = new Sessao
        {
            Id = Guid.NewGuid(),
            CriadaEm = DateTime.UtcNow,
            UltimoAcesso = DateTime.UtcNow,
        };

        _db.Sessoes.Add(sessao);
        await _db.SaveChangesAsync();

        _sessaoAtual.Id = sessao.Id;
        await DadosDemo.Popular(_db, sessao.Id);

        return sessao;
    }

    // Volta a sessao para o estado inicial (botao "Reiniciar demonstracao" do front).
    public async Task Reiniciar(Guid id)
    {
        await ApagarDados(_db, id);
        _db.ChangeTracker.Clear();
        await DadosDemo.Popular(_db, id);
    }

    // Apaga os dados de uma sessao, de quem depende para quem e dependido:
    // itens -> notas -> entregas -> clientes e motoristas.
    // IgnoreQueryFilters: aqui queremos enxergar a sessao informada, nao a da requisicao.
    public static async Task ApagarDados(AppDbContext db, Guid id, CancellationToken cancelar = default)
    {
        await db.ItensNota.IgnoreQueryFilters().Where(i => i.SessaoId == id).ExecuteDeleteAsync(cancelar);
        await db.NotasFiscais.IgnoreQueryFilters().Where(n => n.SessaoId == id).ExecuteDeleteAsync(cancelar);
        await db.Entregas.IgnoreQueryFilters().Where(e => e.SessaoId == id).ExecuteDeleteAsync(cancelar);
        await db.Clientes.IgnoreQueryFilters().Where(c => c.SessaoId == id).ExecuteDeleteAsync(cancelar);
        await db.Motoristas.IgnoreQueryFilters().Where(m => m.SessaoId == id).ExecuteDeleteAsync(cancelar);
    }
}
