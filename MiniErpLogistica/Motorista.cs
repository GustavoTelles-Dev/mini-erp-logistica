public class Motorista
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cnh { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public List<Entrega> Entregas { get; set; } = new();
}