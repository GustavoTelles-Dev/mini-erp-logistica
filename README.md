# Mini ERP de Logística

Sistema de gestão logística para controle de clientes, motoristas e entregas, escrito em C# com .NET 8.

Comecei este projeto como uma aplicação de console, para firmar a lógica de negócio e a persistência de dados, e depois o evoluí para uma **API REST**. Toda a camada de domínio (as entidades e o acesso ao banco) foi reaproveitada entre as duas versões — a ideia era justamente construir uma base sólida primeiro e trocar só a "porta de entrada" depois, sem reescrever o núcleo.

## O que o sistema faz

O domínio gira em torno de **entregas**. Cada entrega pertence a um cliente e, quando é despachada, passa a ter um motorista responsável. O status acompanha o ciclo de vida da entrega: nasce como *Pendente*, vai para *Em trânsito* no despacho e termina como *Entregue*.

As entidades e como se relacionam:

- **Cliente** — quem solicita a entrega. Um cliente pode ter várias entregas.
- **Motorista** — quem despacha. Um motorista pode estar em várias entregas.
- **Entrega** — pertence a um cliente (obrigatório) e a um motorista (opcional, definido no despacho).

## Duas versões

O repositório tem dois projetos que compartilham as mesmas entidades:

- **`MiniErpLogistica`** — versão console, com menu interativo no terminal. Foi onde a lógica e as validações nasceram.
- **`MiniErpApi`** — versão API REST (ASP.NET Core Minimal API), que expõe as operações como endpoints HTTP e pode ser consumida por outras aplicações.

## Tecnologias

- **C# / .NET 8** (LTS)
- **ASP.NET Core Minimal API** — camada HTTP
- **Entity Framework Core 8** — ORM (mapeamento objeto-relacional)
- **SQLite** — banco de dados
- **Swagger** — documentação e teste dos endpoints

O banco foi mantido em SQLite pela simplicidade, mas como o acesso passa pelo EF Core, migrar para PostgreSQL ou SQL Server exige mudar essencialmente a linha de configuração do provedor.

## Conceitos que o projeto exercita

- CRUD completo (criar, ler, atualizar, excluir)
- Relacionamentos entre entidades (um-para-muitos)
- Chaves estrangeiras opcionais (`int?`) — o motorista só é vinculado no despacho
- Integridade referencial — não é possível excluir um cliente ou motorista com entregas vinculadas
- Validação de dados de entrada
- Persistência com migrations do EF Core
- API REST com verbos HTTP e códigos de status adequados (200, 201, 204, 400, 404)
- Tratamento de ciclo de referência na serialização JSON

## Como executar

**Pré-requisito:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### API

```bash
cd MiniErpApi
dotnet restore
dotnet ef database update   # cria o banco a partir das migrations
dotnet run
```

Com a aplicação no ar, a documentação interativa fica em:

```
http://localhost:5185/swagger
```

> A porta pode variar; confira o endereço em `Now listening on:` no terminal.

### Console

```bash
cd MiniErpLogistica
dotnet ef database update
dotnet run
```

## Endpoints da API

| Método | Rota               | Descrição                                        |
|--------|--------------------|--------------------------------------------------|
| GET    | `/clientes`        | Lista os clientes                                |
| POST   | `/clientes`        | Cadastra um cliente                              |
| PUT    | `/clientes/{id}`   | Atualiza um cliente                              |
| DELETE | `/clientes/{id}`   | Exclui um cliente (bloqueado se tiver entregas)  |
| GET    | `/motoristas`      | Lista os motoristas                              |
| GET    | `/motoristas/{id}` | Busca um motorista pelo id                       |
| POST   | `/motoristas`      | Cadastra um motorista                            |
| PUT    | `/motoristas/{id}` | Atualiza um motorista                            |
| DELETE | `/motoristas/{id}` | Exclui um motorista (bloqueado se tiver entregas)|
| GET    | `/entregas`        | Lista as entregas com cliente e motorista        |
| POST   | `/entregas`        | Cadastra uma entrega vinculada a um cliente      |
| PUT    | `/entregas/{id}`   | Atualiza o status; no despacho, vincula motorista|
| DELETE | `/entregas/{id}`   | Exclui uma entrega                               |

## Estrutura do repositório

```
mini ERP logistico/
├── MiniErpLogistica/   # versão console
│   ├── Cliente.cs
│   ├── Motorista.cs
│   ├── Entrega.cs
│   ├── AppDbContext.cs
│   └── Program.cs
└── MiniErpApi/         # versão API REST
    ├── Cliente.cs
    ├── Motorista.cs
    ├── Entrega.cs
    ├── AppDbContext.cs
    └── Program.cs
```

## Roadmap

Este é o estado da **V1**. As próximas versões estão planejadas para evoluir o domínio:

- **V2 — Veículo:** cadastro de frota, ligando motorista, veículo e entrega.
- **V3 — Histórico de status:** registrar cada mudança de status de uma entrega ao longo do tempo, não apenas o status atual.
- **Futuro:** camada visual (front-end) consumindo a API, autenticação, e validação de entregas duplicadas.

## Autor

**Gustavo Delatore Telles**

---

Projeto de estudo em C#, .NET e desenvolvimento de APIs REST.
