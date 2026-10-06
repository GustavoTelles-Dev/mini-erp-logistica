using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    private readonly SessaoAtual _sessaoAtual;

    // O DbContext agora chega pronto por injecao de dependencia (configurado no Program.cs),
    // em vez de cada endpoint fazer "new AppDbContext()".
    public AppDbContext(DbContextOptions<AppDbContext> options, SessaoAtual sessaoAtual) : base(options)
    {
        _sessaoAtual = sessaoAtual;
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Entrega> Entregas { get; set; }
    public DbSet<Motorista> Motoristas { get; set; }
    public DbSet<Sessao> Sessoes { get; set; }
    public DbSet<NotaFiscal> NotasFiscais { get; set; }
    public DbSet<ItemNota> ItensNota { get; set; }

    // Lido pelos filtros globais abaixo, a cada consulta.
    private Guid SessaoDaRequisicao => _sessaoAtual.Id;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Filtro global: toda consulta ja sai filtrada pela sessao do visitante.
        // E isso que impede um visitante de ver (ou apagar) os dados de outro.
        modelBuilder.Entity<Cliente>().HasQueryFilter(c => c.SessaoId == SessaoDaRequisicao);
        modelBuilder.Entity<Motorista>().HasQueryFilter(m => m.SessaoId == SessaoDaRequisicao);
        modelBuilder.Entity<Entrega>().HasQueryFilter(e => e.SessaoId == SessaoDaRequisicao);
        modelBuilder.Entity<NotaFiscal>().HasQueryFilter(n => n.SessaoId == SessaoDaRequisicao);
        modelBuilder.Entity<ItemNota>().HasQueryFilter(i => i.SessaoId == SessaoDaRequisicao);

        // Notas fiscais: valores em dinheiro com 2 casas; quantidade com 3 (ex.: 1,250 kg)
        modelBuilder.Entity<NotaFiscal>().HasIndex(n => n.SessaoId);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.ValorTotal).HasPrecision(14, 2);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.ChaveAcesso).HasMaxLength(44);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.Numero).HasMaxLength(20);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.Serie).HasMaxLength(5);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.EmitenteCnpj).HasMaxLength(14);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.EmitenteNome).HasMaxLength(150);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.DestinatarioDocumento).HasMaxLength(14);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.DestinatarioNome).HasMaxLength(150);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.ArquivoTipo).HasMaxLength(40);
        modelBuilder.Entity<NotaFiscal>().Property(n => n.ArquivoNome).HasMaxLength(120);
        modelBuilder.Entity<ItemNota>().Property(i => i.Descricao).HasMaxLength(200);
        modelBuilder.Entity<ItemNota>().Property(i => i.Quantidade).HasPrecision(12, 3);
        modelBuilder.Entity<ItemNota>().Property(i => i.ValorUnitario).HasPrecision(14, 2);
        modelBuilder.Entity<ItemNota>().Property(i => i.ValorTotal).HasPrecision(14, 2);

        // Nota -> Itens: excluir a nota leva os itens junto (cascata).
        modelBuilder.Entity<NotaFiscal>()
            .HasMany(n => n.Itens).WithOne()
            .HasForeignKey(i => i.NotaFiscalId).OnDelete(DeleteBehavior.Cascade);

        // Entrega -> Notas: Restrict (entrega com nota nao pode ser excluida sem antes tirar a nota).
        modelBuilder.Entity<NotaFiscal>()
            .HasOne(n => n.Entrega).WithMany()
            .HasForeignKey(n => n.EntregaId).OnDelete(DeleteBehavior.Restrict);

        // Indices: as consultas sempre filtram por SessaoId; a limpeza filtra por UltimoAcesso.
        modelBuilder.Entity<Cliente>().HasIndex(c => c.SessaoId);
        modelBuilder.Entity<Motorista>().HasIndex(m => m.SessaoId);
        modelBuilder.Entity<Entrega>().HasIndex(e => e.SessaoId);
        modelBuilder.Entity<Sessao>().HasIndex(s => s.UltimoAcesso);

        // Concorrencia otimista: o Postgres guarda a "versao" da linha (coluna de sistema xmin).
        // Se duas abas despacharem a mesma entrega ao mesmo tempo, a segunda recebe erro de conflito
        // (409) em vez de sobrescrever a primeira sem ninguem perceber.
        modelBuilder.Entity<Entrega>().Property<uint>("Versao").IsRowVersion();

        // Status gravado como texto ("Pendente", "EmTransito"...) para ficar legivel no Supabase.
        modelBuilder.Entity<Entrega>().Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        // Tamanhos maximos: viram limites nas colunas do banco.
        modelBuilder.Entity<Cliente>().Property(c => c.Nome).HasMaxLength(120);
        modelBuilder.Entity<Motorista>().Property(m => m.Nome).HasMaxLength(120);
        modelBuilder.Entity<Motorista>().Property(m => m.Cnh).HasMaxLength(11);
        modelBuilder.Entity<Motorista>().Property(m => m.Telefone).HasMaxLength(20);
        modelBuilder.Entity<Entrega>().Property(e => e.Endereco).HasMaxLength(200);

        // Restrict: o proprio banco tambem impede excluir cliente/motorista que tem entregas.
        // (A API ja checa antes e devolve uma mensagem amigavel; o banco e a ultima barreira.)
        modelBuilder.Entity<Entrega>()
            .HasOne(e => e.Cliente).WithMany()
            .HasForeignKey(e => e.ClienteId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Entrega>()
            .HasOne(e => e.Motorista).WithMany(m => m.Entregas)
            .HasForeignKey(e => e.MotoristaId).OnDelete(DeleteBehavior.Restrict);
    }

    // Antes de salvar, carimba a sessao atual em todo registro novo.
    // Assim nenhum endpoint precisa lembrar de preencher o SessaoId.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entrada in ChangeTracker.Entries<IDaSessao>())
        {
            if (entrada.State == EntityState.Added && entrada.Entity.SessaoId == Guid.Empty)
            {
                entrada.Entity.SessaoId = _sessaoAtual.Id;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
