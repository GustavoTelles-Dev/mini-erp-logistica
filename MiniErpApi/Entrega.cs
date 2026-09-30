public class Entrega
{
    public int Id { get; set; }
    public string Endereco { get; set; } = string.Empty;
    public string Status { get; set; } = "Pendente";
    public int ClienteId { get; set; }
    public Cliente Cliente { get; set; } = null!;
    public int? MotoristaId { get; set; }
    public Motorista? Motorista { get; set; }
}