// Mudanca de status: despachar (EmTransito + motorista) ou concluir (Entregue).
public class StatusEntrada
{
    public StatusEntrega? Status { get; set; }
    public int? MotoristaId { get; set; }
}
