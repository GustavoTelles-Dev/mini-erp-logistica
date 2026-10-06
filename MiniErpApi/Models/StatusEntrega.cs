// Os tres estados possiveis de uma entrega.
// Com enum (e nao texto livre) nao existe status digitado errado, como "Em Transito" ou "entregue".
// No JSON e no banco ele aparece como texto: "Pendente", "EmTransito", "Entregue".
public enum StatusEntrega
{
    Pendente,
    EmTransito,
    Entregue
}
