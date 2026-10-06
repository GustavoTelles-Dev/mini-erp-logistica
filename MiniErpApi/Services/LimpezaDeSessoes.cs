using Microsoft.EntityFrameworkCore;

// Servico em segundo plano: enquanto a API estiver no ar, a cada 30 minutos
// apaga as sessoes paradas ha mais de 6 horas, junto com todos os dados delas.
public class LimpezaDeSessoes : BackgroundService
{
    public const int HorasSemUso = 6;

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<LimpezaDeSessoes> _log;

    public LimpezaDeSessoes(IServiceScopeFactory escopos, ILogger<LimpezaDeSessoes> log)
    {
        _escopos = escopos;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken parar)
    {
        using var relogio = new PeriodicTimer(TimeSpan.FromMinutes(30));

        do
        {
            try
            {
                await Limpar(parar);
            }
            catch (Exception erro) when (!parar.IsCancellationRequested)
            {
                _log.LogError(erro, "Falha ao limpar sessoes antigas.");
            }
        }
        while (await relogio.WaitForNextTickAsync(parar));
    }

    private async Task Limpar(CancellationToken parar)
    {
        // Servico de fundo nao tem requisicao, entao cria o proprio escopo para usar o banco.
        using var escopo = _escopos.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        var limite = DateTime.UtcNow.AddHours(-HorasSemUso);
        var antigas = await db.Sessoes.Where(s => s.UltimoAcesso < limite).Select(s => s.Id).ToListAsync(parar);

        if (antigas.Count == 0)
        {
            return;
        }

        foreach (var id in antigas)
        {
            await GerenciadorDeSessao.ApagarDados(db, id, parar);
        }

        await db.Sessoes.Where(s => antigas.Contains(s.Id)).ExecuteDeleteAsync(parar);
        _log.LogInformation("{Quantidade} sessoes antigas apagadas.", antigas.Count);
    }
}
