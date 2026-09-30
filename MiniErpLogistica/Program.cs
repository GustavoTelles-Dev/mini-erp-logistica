using Microsoft.EntityFrameworkCore;

var db = new AppDbContext();
db.Database.Migrate();

bool executando = true;

while (executando)
{
    Console.WriteLine("");
    Console.WriteLine("=== MINI ERP LOGISTICA ===");
    Console.WriteLine("-- Clientes --");
    Console.WriteLine("1 - Cadastrar cliente");
    Console.WriteLine("2 - Listar clientes");
    Console.WriteLine("3 - Excluir cliente");
    Console.WriteLine("-- Motoristas --");
    Console.WriteLine("4 - Cadastrar motorista");
    Console.WriteLine("5 - Listar motoristas");
    Console.WriteLine("6 - Excluir motorista");
    Console.WriteLine("-- Entregas --");
    Console.WriteLine("7 - Cadastrar entrega");
    Console.WriteLine("8 - Listar entregas");
    Console.WriteLine("9 - Atualizar status de entrega");
    Console.WriteLine("0 - Sair");
    Console.WriteLine("Escolha uma opcao:");

    string opcao = Console.ReadLine() ?? "";

    if (opcao == "1")
    {
        Cliente novoCliente = CadastrarCliente();
        db.Clientes.Add(novoCliente);
        db.SaveChanges();
        Console.WriteLine("Cliente cadastrado com sucesso!");
    }

    else if (opcao == "2")
    {
        var listaDoBanco = db.Clientes.ToList();
        ListarClientes(listaDoBanco);
    }

    else if (opcao == "3")
    {
        ExcluirCliente(db);
    }

    else if (opcao == "4")
    {
        Motorista novoMotorista = CadastrarMotorista();
        db.Motoristas.Add(novoMotorista);
        db.SaveChanges();
        Console.WriteLine("Motorista cadastrado com sucesso!");
    }

    else if (opcao == "5")
    {
        var listaDoBanco = db.Motoristas.ToList();
        ListarMotoristas(listaDoBanco);
    }

    else if (opcao == "6")
    {
        ExcluirMotorista(db);
    }

    else if (opcao == "7")
    {
        Entrega? novaEntrega = CadastrarEntrega(db);
        if (novaEntrega != null)
        {
            db.Entregas.Add(novaEntrega);
            db.SaveChanges();
            Console.WriteLine("Entrega cadastrada com sucesso!");
        }
    }

    else if (opcao == "8")
    {
        ListarEntregas(db);
    }

    else if (opcao == "9")
    {
        AtualizarStatusEntrega(db);
    }

    else if (opcao == "0")
    {
        executando = false;
        Console.WriteLine("Saindo...");
    }

    else
    {
        Console.WriteLine("Opcao invalida.");
    }
}

// ===== CLIENTES =====

void ListarClientes(List<Cliente> clientes)
{
    Console.WriteLine("--- Lista de Clientes ---");
    if (clientes.Count == 0)
    {
        Console.WriteLine("Nenhum cliente cadastrado.");
    }
    else
    {
        foreach (Cliente cliente in clientes)
        {
            Console.WriteLine(cliente.Id + " - " + cliente.Nome + " - " + cliente.Idade + " anos - Ativo: " + cliente.Ativo);
        }
    }
}

Cliente CadastrarCliente()
{
    Cliente novoCliente = new Cliente();

    Console.WriteLine("Nome do cliente:");
    novoCliente.Nome = Console.ReadLine() ?? "";

    int idade;
    Console.WriteLine("Idade do cliente:");
    while (!int.TryParse(Console.ReadLine(), out idade) || idade <= 0)
    {
        Console.WriteLine("Idade invalida! Digite um numero maior que zero:");
    }
    novoCliente.Idade = idade;

    Console.WriteLine("Cliente ativo? [s/n]:");
    string ativoTexto = (Console.ReadLine() ?? "").Trim().ToLower();
    while (ativoTexto != "s" && ativoTexto != "sim" && ativoTexto != "n" && ativoTexto != "nao" && ativoTexto != "não")
    {
        Console.WriteLine("Opcao invalida! Digite [s/n]:");
        ativoTexto = (Console.ReadLine() ?? "").Trim().ToLower();
    }
    novoCliente.Ativo = ativoTexto == "s" || ativoTexto == "sim";

    return novoCliente;
}

void ExcluirCliente(AppDbContext db)
{
    var clientes = db.Clientes.ToList();

    if (clientes.Count == 0)
    {
        Console.WriteLine("Nenhum cliente cadastrado.");
        return;
    }

    Console.WriteLine("--- Clientes ---");
    foreach (Cliente c in clientes)
    {
        Console.WriteLine(c.Id + " - " + c.Nome);
    }

    Console.WriteLine("Digite o ID do cliente que deseja excluir: ");

    Cliente? clienteEscolhido = null;
    while (clienteEscolhido == null)
    {
        int id;
        if (!int.TryParse(Console.ReadLine(), out id))
        {
            Console.WriteLine("ID invalido! Digite um numero:");
            continue;
        }

        clienteEscolhido = db.Clientes.Find(id);
        if (clienteEscolhido == null)
        {
            Console.WriteLine("Nao existe cliente com esse ID. Tente novamente:");
        }
    }

    bool temEntregas = db.Entregas.Any(e => e.ClienteId == clienteEscolhido.Id);
    if (temEntregas)
    {
        Console.WriteLine("Este cliente possui entregas cadastradas e não pode ser excluido.");
        return;
    }

    db.Clientes.Remove(clienteEscolhido);
    db.SaveChanges();
    Console.WriteLine("Cliente excluido com sucesso!");
}

// ===== MOTORISTAS =====

void ListarMotoristas(List<Motorista> motoristas)
{
    Console.WriteLine("--- Lista de Motoristas ---");
    if (motoristas.Count == 0)
    {
        Console.WriteLine("Nenhum motorista cadastrado.");
    }
    else
    {
        foreach (Motorista motorista in motoristas)
        {
            Console.WriteLine(motorista.Id + " - " + motorista.Nome + " - CNH: " + motorista.Cnh + " - Tel: " + motorista.Telefone);
        }
    }
}

Motorista CadastrarMotorista()
{
    Motorista novoMotorista = new Motorista();

    Console.WriteLine("Nome do motorista:");
    novoMotorista.Nome = Console.ReadLine() ?? "";

    Console.WriteLine("CNH do motorista:");
    novoMotorista.Cnh = Console.ReadLine() ?? "";

    Console.WriteLine("Telefone do motorista:");
    novoMotorista.Telefone = Console.ReadLine() ?? "";

    return novoMotorista;
}

void ExcluirMotorista(AppDbContext db)
{
    var motoristas = db.Motoristas.ToList();

    if (motoristas.Count == 0)
    {
        Console.WriteLine("Nenhum motorista cadastrado.");
        return;
    }

    Console.WriteLine("--- Motoristas ---");
    foreach (Motorista m in motoristas)
    {
        Console.WriteLine(m.Id + " - " + m.Nome);
    }

    Console.WriteLine("Digite o ID do motorista que deseja excluir: ");

    Motorista? motoristaEscolhido = null;
    while (motoristaEscolhido == null)
    {
        int id;
        if (!int.TryParse(Console.ReadLine(), out id))
        {
            Console.WriteLine("ID invalido! Digite um numero:");
            continue;
        }

        motoristaEscolhido = db.Motoristas.Find(id);
        if (motoristaEscolhido == null)
        {
            Console.WriteLine("Nao existe motorista com esse ID. Tente novamente:");
        }
    }

    bool temEntregas = db.Entregas.Any(e => e.MotoristaId == motoristaEscolhido.Id);
    if (temEntregas)
    {
        Console.WriteLine("Este motorista possui entregas vinculadas e não pode ser excluido.");
        return;
    }

    db.Motoristas.Remove(motoristaEscolhido);
    db.SaveChanges();
    Console.WriteLine("Motorista excluido com sucesso!");
}

// ===== ENTREGAS =====

Entrega? CadastrarEntrega(AppDbContext db)
{
    Console.WriteLine("--- Clientes disponiveis ---");
    var clientes = db.Clientes.ToList();

    if (clientes.Count == 0)
    {
        Console.WriteLine("Nenhum cliente cadastrado. Cadastre um cliente antes.");
        return null;
    }

    foreach (Cliente c in clientes)
    {
        Console.WriteLine(c.Id + " - " + c.Nome);
    }

    Console.WriteLine("Digite o ID do cliente dono da entrega:");

    Cliente? clienteEscolhido = null;
    while (clienteEscolhido == null)
    {
        int clienteId;
        if (!int.TryParse(Console.ReadLine(), out clienteId))
        {
            Console.WriteLine("ID invalido! Digite um numero:");
            continue;
        }

        clienteEscolhido = db.Clientes.Find(clienteId);
        if (clienteEscolhido == null)
        {
            Console.WriteLine("Nao existe cliente com esse ID. Tente novamente:");
        }
    }

    Entrega novaEntrega = new Entrega();
    novaEntrega.ClienteId = clienteEscolhido.Id;

    Console.WriteLine("Endereco de entrega:");
    novaEntrega.Endereco = Console.ReadLine() ?? "";

    return novaEntrega;
}

void ListarEntregas(AppDbContext db)
{
    Console.WriteLine("--- Lista de Entregas ---");

    var entregas = db.Entregas.Include(e => e.Cliente).Include(e => e.Motorista).ToList();

    if (entregas.Count == 0)
    {
        Console.WriteLine("Nenhuma entrega cadastrada.");
    }
    else
    {
        foreach (Entrega entrega in entregas)
        {
            string motoristaNome = entrega.Motorista == null ? "Sem motorista" : entrega.Motorista.Nome;
            Console.WriteLine(entrega.Id + " - " + entrega.Endereco + " - " + entrega.Status + " - Cliente: " + entrega.Cliente.Nome + " - Motorista: " + motoristaNome);
        }
    }
}

void AtualizarStatusEntrega(AppDbContext db)
{
    var entregas = db.Entregas.Include(e => e.Cliente).Include(e => e.Motorista).ToList();

    if (entregas.Count == 0)
    {
        Console.WriteLine("Nenhuma entrega cadastrada.");
        return;
    }

    Console.WriteLine("--- Entregas ---");
    foreach (Entrega e in entregas)
    {
        string motoristaNome = e.Motorista == null ? "Sem motorista" : e.Motorista.Nome;
        Console.WriteLine(e.Id + " - " + e.Endereco + " - " + e.Status + " - Cliente: " + e.Cliente.Nome + " - Motorista: " + motoristaNome);
    }

    Console.WriteLine("Digite o ID da entrega que deseja atualizar:");

    Entrega? entregaEscolhida = null;
    while (entregaEscolhida == null)
    {
        int id;
        if (!int.TryParse(Console.ReadLine(), out id))
        {
            Console.WriteLine("ID invalido! Digite um numero:");
            continue;
        }

        entregaEscolhida = db.Entregas.Find(id);
        if (entregaEscolhida == null)
        {
            Console.WriteLine("Nao existe entrega com esse ID. Tente novamente: ");
        }
    }

    Console.WriteLine("Escolha o novo status:");
    Console.WriteLine("1 - Pendente");
    Console.WriteLine("2 - Em transito");
    Console.WriteLine("3 - Entregue");

    string opcaoStatus = "";
    while (opcaoStatus != "1" && opcaoStatus != "2" && opcaoStatus != "3")
    {
        opcaoStatus = Console.ReadLine() ?? "";
        if (opcaoStatus != "1" && opcaoStatus != "2" && opcaoStatus != "3")
        {
            Console.WriteLine("Opcao invalida! Digite [1/2 ou 3]: ");
        }
    }

    if (opcaoStatus == "1")
    {
        entregaEscolhida.Status = "Pendente";
    }
    else if (opcaoStatus == "2")
    {
        entregaEscolhida.Status = "Em transito";

        var motoristas = db.Motoristas.ToList();
        if (motoristas.Count == 0)
        {
            Console.WriteLine("Nenhum motorista cadastrado. Cadastre um motorista antes de despachar.");
            return;
        }

        Console.WriteLine("--- Motoristas disponiveis ---");
        foreach (Motorista m in motoristas)
        {
            Console.WriteLine(m.Id + " - " + m.Nome);
        }

        Console.WriteLine("Digite o ID do motorista que vai despachar a entrega:");

        Motorista? motoristaEscolhido = null;
        while (motoristaEscolhido == null)
        {
            int motoristaId;
            if (!int.TryParse(Console.ReadLine(), out motoristaId))
            {
                Console.WriteLine("ID invalido! Digite um numero:");
                continue;
            }

            motoristaEscolhido = db.Motoristas.Find(motoristaId);
            if (motoristaEscolhido == null)
            {
                Console.WriteLine("Nao existe motorista com esse ID. Tente novamente:");
            }
        }

        entregaEscolhida.MotoristaId = motoristaEscolhido.Id;
    }
    else
    {
        entregaEscolhida.Status = "Entregue";
    }

    db.SaveChanges();
    Console.WriteLine("Status atualizado com sucesso!");
}
