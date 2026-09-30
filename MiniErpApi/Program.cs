using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Configura o serializador JSON para ignorar ciclos de referencia.
// Necessario porque Entrega -> Motorista -> Entregas -> Motorista... formam um
// ciclo (relacionamento de duas vias). Sem isso, o GET /entregas estoura com erro.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

//Endpoint Clientes
app.MapGet("/clientes", () =>
{
    var db = new AppDbContext();
    var clientes = db.Clientes.ToList();

    return clientes;
});

app.MapPost("/clientes", (Cliente novoCliente) =>
{
    var db = new AppDbContext();

    db.Clientes.Add(novoCliente);
    db.SaveChanges();

    return Results.Created($"/clientes/{novoCliente.Id}", novoCliente);
});

app.MapPut("/clientes/{id}", (int id, Cliente dadosAtualizados) =>
{
    var db = new AppDbContext();
    var cliente = db.Clientes.Find(id);

    if (cliente == null)
    {
        return Results.NotFound();
    }

    cliente.Nome = dadosAtualizados.Nome;
    cliente.Idade = dadosAtualizados.Idade;
    cliente.Ativo = dadosAtualizados.Ativo;

    db.SaveChanges();

    return Results.Ok(cliente);
});

app.MapDelete("/clientes/{id}", (int id) =>
{
    var db = new AppDbContext();
    var cliente = db.Clientes.Find(id);

    if (cliente == null)
    {
        return Results.NotFound();
    }

    db.Clientes.Remove(cliente);
    db.SaveChanges();

    return Results.NoContent();
});

//Endpoint Entregas
app.MapGet("/entregas", () =>
{
    var db = new AppDbContext();
    // Include traz junto o Cliente e o Motorista de cada entrega (relacionamentos),
    // para que ambos apareçam no JSON de resposta. Sem o Include eles viriam nulos.
    var entregas = db.Entregas.Include(e => e.Cliente).Include(e => e.Motorista).ToList();

    return entregas;
});

app.MapPost("/entregas", (Entrega novaEntrega) =>
{
    var db = new AppDbContext();

    // Valida se o cliente informado existe. Cliente e obrigatorio na entrega.
    var clienteExiste = db.Clientes.Any(c => c.Id == novaEntrega.ClienteId);

    if (!clienteExiste)
    {
        return Results.BadRequest("Cliente nao encontrado.");
    }

    // Motorista e opcional (MotoristaId e int?). So valida se um motorista foi informado.
    // Se veio um MotoristaId que nao existe, recusa a requisicao.
    if (novaEntrega.MotoristaId != null)
    {
        var motoristaExiste = db.Motoristas.Any(m => m.Id == novaEntrega.MotoristaId);
        if (!motoristaExiste)
        {
            return Results.BadRequest("Motorista nao encontrado.");
        }
    }

    // Limpa as propriedades de navegacao para o EF nao tentar criar Cliente/Motorista
    // novos junto. A ligacao acontece pelas chaves estrangeiras (ClienteId / MotoristaId).
    novaEntrega.Cliente = null!;
    novaEntrega.Motorista = null;

    db.Entregas.Add(novaEntrega);
    db.SaveChanges();

    return Results.Created($"/entregas/{novaEntrega.Id}", novaEntrega);
});

// PUT de entrega: atualiza o status e, no "despacho", vincula um motorista.
// Regra de negocio: ao mudar o status para "Em transito" a entrega esta sendo
// despachada, entao um MotoristaId deve ser informado e precisa existir.
app.MapPut("/entregas/{id}", (int id, Entrega dadosAtualizados) =>
{
    var db = new AppDbContext();
    var entrega = db.Entregas.Find(id);

    if (entrega == null)
    {
        return Results.NotFound();
    }

    // Se a entrega esta sendo despachada, o motorista passa a ser obrigatorio.
    if (dadosAtualizados.Status == "Em transito")
    {
        if (dadosAtualizados.MotoristaId == null)
        {
            return Results.BadRequest("Para despachar (Em transito) e necessario informar o motorista.");
        }

        var motoristaExiste = db.Motoristas.Any(m => m.Id == dadosAtualizados.MotoristaId);
        if (!motoristaExiste)
        {
            return Results.BadRequest("Motorista nao encontrado.");
        }

        entrega.MotoristaId = dadosAtualizados.MotoristaId;
    }

    entrega.Status = dadosAtualizados.Status;
    db.SaveChanges();

    return Results.Ok(entrega);
});

// DELETE de entrega: remove a entrega pelo Id. A entrega esta na ponta do
// relacionamento (nada depende dela), entao pode ser excluida diretamente.
app.MapDelete("/entregas/{id}", (int id) =>
{
    var db = new AppDbContext();
    var entrega = db.Entregas.Find(id);

    if (entrega == null)
    {
        return Results.NotFound();
    }

    db.Entregas.Remove(entrega);
    db.SaveChanges();

    return Results.NoContent();
});

//Endpoint Motorista
app.MapGet("/motoristas", () =>
{
    var db = new AppDbContext();
    var motoristas = db.Motoristas.ToList();

    return motoristas;
});

app.MapGet("/motoristas/{id}", (int id) =>
{
    var db = new AppDbContext();
    var motorista = db.Motoristas.Find(id);

    if (motorista == null)
    {
        return Results.NotFound();
    }

    return Results.Ok(motorista);
});

app.MapPost("/motoristas", (Motorista novoMotorista) =>
{
    var db = new AppDbContext();

    db.Motoristas.Add(novoMotorista);
    db.SaveChanges();

    return Results.Created($"/motoristas/{novoMotorista.Id}", novoMotorista);
});

app.MapPut("/motoristas/{id}", (int id, Motorista dadosAtualizados) =>
{
    var db = new AppDbContext();
    var motorista = db.Motoristas.Find(id);

    if (motorista == null)
    {
        return Results.NotFound();
    }

    motorista.Nome = dadosAtualizados.Nome;
    motorista.Cnh = dadosAtualizados.Cnh;
    motorista.Telefone = dadosAtualizados.Telefone;

    db.SaveChanges();

    return Results.Ok(motorista);
});

app.MapDelete("/motoristas/{id}", (int id) =>
{
    var db = new AppDbContext();
    var motorista = db.Motoristas.Find(id);

    if (motorista == null)
    {
        return Results.NotFound();
    }

    // Protecao de integridade: nao permite excluir um motorista que ja tem
    // entregas vinculadas, para nao deixar essas entregas apontando para ninguem.
    bool temEntregas = db.Entregas.Any(e => e.MotoristaId == motorista.Id);
    if (temEntregas)
    {
        return Results.BadRequest("Este motorista possui entregas vinculadas e nao pode ser excluido.");
    }

    db.Motoristas.Remove(motorista);
    db.SaveChanges();

    return Results.NoContent();
});

app.Run();