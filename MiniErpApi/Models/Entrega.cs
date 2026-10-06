using System.Text.Json.Serialization;

// Entrega: pertence a um cliente (obrigatorio) e ganha um motorista no despacho.
public class Entrega : IDaSessao
{
    public int Id { get; set; }
    public string Endereco { get; set; } = string.Empty;
    public StatusEntrega Status { get; set; } = StatusEntrega.Pendente;

    // Datas do ciclo de vida, sempre em UTC (o front converte para o horario local).
    // Sao elas que alimentam os graficos da semana e o historico no detalhe da entrega.
    public DateTime CriadaEm { get; set; }
    public DateTime? DespachadaEm { get; set; }
    public DateTime? EntregueEm { get; set; }

    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;

    public int? MotoristaId { get; set; }
    public Motorista? Motorista { get; set; }

    [JsonIgnore]
    public Guid SessaoId { get; set; }
}
