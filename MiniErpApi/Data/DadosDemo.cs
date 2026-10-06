// Dados de exemplo que cada visitante recebe ao abrir o sistema.
// As datas sao relativas a "agora", para os graficos da semana sempre terem movimento.
public static class DadosDemo
{
    public static async Task Popular(AppDbContext db, Guid sessaoId)
    {
        var agora = DateTime.UtcNow;

        var clientes = new List<Cliente>
        {
            new Cliente { Nome = "Maria Silva", Idade = 34, Ativo = true, SessaoId = sessaoId },
            new Cliente { Nome = "José Carlos", Idade = 41, Ativo = true, SessaoId = sessaoId },
            new Cliente { Nome = "Alberto Nóbrega", Idade = 58, Ativo = false, SessaoId = sessaoId },
            new Cliente { Nome = "Renata Lopes", Idade = 29, Ativo = true, SessaoId = sessaoId },
            new Cliente { Nome = "Distribuidora Piracicaba", Idade = 37, Ativo = true, SessaoId = sessaoId },
            new Cliente { Nome = "Ana Beatriz Costa", Idade = 27, Ativo = true, SessaoId = sessaoId },
        };

        var motoristas = new List<Motorista>
        {
            new Motorista { Nome = "João Souza", Cnh = "12345678900", Telefone = "(19) 99812-0451", SessaoId = sessaoId },
            new Motorista { Nome = "Pedro Ramos", Cnh = "98765432100", Telefone = "(19) 99734-2210", SessaoId = sessaoId },
            new Motorista { Nome = "Carlos Nunes", Cnh = "45678912300", Telefone = "(19) 99655-7782", SessaoId = sessaoId },
            new Motorista { Nome = "Lucas Ferreira", Cnh = "32165498700", Telefone = "(19) 99501-3348", SessaoId = sessaoId },
        };

        db.Clientes.AddRange(clientes);
        db.Motoristas.AddRange(motoristas);
        await db.SaveChangesAsync(); // salva primeiro para o banco gerar os Ids

        // Cada linha: indice do cliente, endereco, ha quantos dias foi criada, status final, indice do motorista
        var roteiro = new (int cliente, string endereco, double diasAtras, StatusEntrega status, int motorista)[]
        {
            (0, "Rua das Flores, 123 - Centro, Limeira", 6.5, StatusEntrega.Entregue, 0),
            (4, "Av. Campinas, 2200 - Distrito Industrial, Limeira", 6.4, StatusEntrega.Entregue, 1),
            (1, "Av. Brasil, 890 - Jd. América, Limeira", 5.6, StatusEntrega.Entregue, 2),
            (2, "Rua Sete de Setembro, 51 - Centro, Rio Claro", 5.5, StatusEntrega.Entregue, 0),
            (3, "Rua XV de Novembro, 400 - Centro, Americana", 5.3, StatusEntrega.Entregue, 1),
            (4, "Rod. Anhanguera, km 152 - Galpão 3, Limeira", 4.4, StatusEntrega.Entregue, 2),
            (5, "Rua Gomes, 75 - Vila Nova, Piracicaba", 3.6, StatusEntrega.Entregue, 0),
            (0, "Al. Santos, 12 - Boa Vista, Limeira", 3.5, StatusEntrega.Entregue, 1),
            (1, "Rua Boa Morte, 980 - Centro, Limeira", 3.4, StatusEntrega.Entregue, 2),
            (4, "Av. Independência, 1500 - Cidade Alta, Piracicaba", 3.2, StatusEntrega.Entregue, 3),
            (3, "Rua Tiradentes, 310 - Vila Cláudia, Limeira", 2.5, StatusEntrega.Entregue, 0),
            (5, "Rua do Rosário, 455 - Centro, Piracicaba", 2.3, StatusEntrega.Entregue, 1),
            (0, "Av. Saudade, 1020 - Vila Glória, Limeira", 2.1, StatusEntrega.Entregue, 2),
            (1, "Rua Treze de Maio, 77 - Centro, Rio Claro", 1.5, StatusEntrega.Entregue, 3),
            (5, "Av. de Cillo, 2700 - Jd. Ipiranga, Americana", 1.2, StatusEntrega.EmTransito, 2),
            (0, "Rua Barão de Campinas, 88 - Centro, Limeira", 0.7, StatusEntrega.EmTransito, 1),
            (4, "Rua Carlos Gomes, 640 - Centro, Limeira", 0.5, StatusEntrega.Entregue, 0),
            (1, "Rua Paraná, 230 - Vila Paulista, Limeira", 0.25, StatusEntrega.Pendente, -1),
            (3, "Av. Major José Levy Sobrinho, 1200 - Limeira", 0.1, StatusEntrega.Pendente, -1),
        };

        foreach (var item in roteiro)
        {
            var criada = agora.AddDays(-item.diasAtras);

            var entrega = new Entrega
            {
                Endereco = item.endereco,
                Status = item.status,
                CriadaEm = criada,
                ClienteId = clientes[item.cliente].Id,
                SessaoId = sessaoId,
            };

            // Despachada 3h depois de criada; entregue 9h depois (sempre no passado pelo roteiro acima)
            if (item.status != StatusEntrega.Pendente)
            {
                entrega.MotoristaId = motoristas[item.motorista].Id;
                entrega.DespachadaEm = criada.AddHours(3);
            }

            if (item.status == StatusEntrega.Entregue)
            {
                entrega.EntregueEm = criada.AddHours(9);
            }

            db.Entregas.Add(entrega);
        }

        await db.SaveChangesAsync();
    }
}
