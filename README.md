# Mini ERP de Logística

Sistema de gestão logística para controle de clientes, motoristas e entregas, escrito em C# com .NET 8, com banco PostgreSQL no Supabase e uma interface web própria, o **Rota ERP**.

Comecei este projeto como uma aplicação de console, para firmar a lógica de negócio e a persistência de dados, e depois o evoluí para uma **API REST**. A ideia sempre foi construir uma base sólida primeiro e ir trocando as "portas de entrada" (console, API, interface web) sem reescrever o núcleo.

## O que o sistema faz

O domínio gira em torno de **entregas**. Cada entrega pertence a um cliente e, quando é despachada, passa a ter um motorista responsável. O status segue um ciclo de vida fixo, sem pular etapas:

```
Pendente  ──(despacho, exige motorista)──▶  Em trânsito  ──(confirmação)──▶  Entregue
```

Cada etapa registra a data em que aconteceu. São essas datas que alimentam os gráficos da semana no painel e o histórico no detalhe de cada entrega.

As entidades e como se relacionam:

- **Cliente** — quem solicita a entrega. Um cliente pode ter várias entregas; cliente inativo não recebe entregas novas.
- **Motorista** — quem leva a entrega. Um motorista pode estar em várias entregas.
- **Entrega** — pertence a um cliente (obrigatório) e a um motorista (opcional, definido no despacho).

## Duas versões + interface web

- **`MiniErpLogistica`** — versão console (SQLite), com menu interativo no terminal. Foi onde a lógica e as validações nasceram, e fica no repositório como registro desse começo.
- **`MiniErpApi`** — a versão atual: API REST em ASP.NET Core Minimal API, banco PostgreSQL (Supabase) e a interface web **Rota ERP** servida pela própria API (`wwwroot/index.html`).

O Rota ERP é uma página única em HTML, CSS e JavaScript puro, sem framework:

- Painel com indicadores, fluxo semanal (cadastradas × entregues), status das entregas e fila de despacho
- Entregas em lista + detalhe, com despacho (escolha do motorista), confirmação e linha do tempo com datas
- Cadastro, edição e exclusão de clientes e motoristas, com os erros de validação da API aparecendo no campo certo
- Tema claro/escuro, layout responsivo (desktop, tablet e celular) e navegação por teclado nos gráficos

## Demonstração isolada por visitante

Como o projeto fica público para qualquer pessoa testar, cada visitante ganha uma **sessão própria (sandbox)**, já com dados de exemplo:

- O front chama `GET /sessao`, guarda o Id na aba do navegador e envia o header `X-Sessao` em toda chamada.
- Todas as tabelas têm uma coluna `SessaoId`, e o Entity Framework aplica um **filtro global** (`HasQueryFilter`) em toda consulta. Um visitante nunca enxerga nem altera os dados de outro — o mesmo princípio de isolamento (multi-tenant) usado por sistemas SaaS.
- Ao salvar, o `AppDbContext` carimba o `SessaoId` sozinho em todo registro novo; nenhum endpoint precisa lembrar disso.
- Um serviço em segundo plano (`LimpezaDeSessoes`) apaga as sessões paradas há mais de 6 horas. Fechou a aba, a próxima visita começa do zero.
- A criação de sessões tem limite de requisições por IP.

## Tecnologias

- **C# / .NET 8** (LTS)
- **ASP.NET Core Minimal API** — camada HTTP, endpoints organizados por grupo
- **Entity Framework Core 8** + **Npgsql** — ORM e provedor PostgreSQL
- **PostgreSQL (Supabase)** — banco de dados
- **Swagger** — documentação e teste dos endpoints
- **HTML, CSS e JavaScript** — interface web (Rota ERP)

## Conceitos que o projeto exercita

- CRUD completo com código **assíncrono** (`async`/`await`) de ponta a ponta
- **Injeção de dependência** (o `AppDbContext` e os serviços chegam prontos em cada endpoint)
- **DTOs de entrada** com validação, separando o que o cliente envia da entidade salva no banco
- Respostas de erro padronizadas em **Problem Details** (RFC 9457): erros por campo (400) e regras de negócio
- `enum` para o status e **regra de transição** do ciclo de vida da entrega
- Relacionamentos um-para-muitos, chave estrangeira opcional e integridade referencial (API + banco)
- **Multi-tenant** com filtros globais do EF Core, middleware e serviço em segundo plano (`BackgroundService`)
- Limite de requisições (**rate limiting**) e configuração segura (connection string fora do código)
- Migrations aplicadas automaticamente ao iniciar a aplicação

## Como executar

**Pré-requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e um projeto no [Supabase](https://supabase.com) (o plano gratuito serve).

1. No Supabase, abra **Connect**, escolha o tipo **.NET** e o método **Session pooler**, e copie a connection string (com a sua senha).
2. Guarde a string nos *user-secrets* — ela fica fora da pasta do projeto e nunca vai para o Git:

```bash
cd MiniErpApi
dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Database=postgres;Username=...;Password=...;..."
```

3. Rode a API. Na primeira execução as tabelas são criadas sozinhas (migrations automáticas):

```bash
dotnet run
```

Com a aplicação no ar:

```
http://localhost:5185           # interface web (Rota ERP)
http://localhost:5185/swagger   # documentação interativa da API
```

> No Swagger, chame `GET /sessao` primeiro: ele cria a sua sessão e grava um cookie, e os outros endpoints passam a funcionar.
> O arquivo `MiniErpApi/MiniErpApi.http` tem todas as chamadas prontas para testar pelo VS Code.

### Console (V1)

```bash
cd MiniErpLogistica
dotnet ef database update
dotnet run
```

## Endpoints da API

Todos os endpoints de dados exigem a sessão (`X-Sessao` ou cookie).

| Método | Rota                  | Descrição                                                    |
|--------|-----------------------|--------------------------------------------------------------|
| GET    | `/sessao`             | Devolve a sessão do visitante (ou cria uma com dados de exemplo) |
| POST   | `/sessao/reiniciar`   | Volta a sessão aos dados de exemplo                          |
| GET    | `/clientes`           | Lista os clientes                                            |
| POST   | `/clientes`           | Cadastra um cliente                                          |
| PUT    | `/clientes/{id}`      | Atualiza um cliente                                          |
| DELETE | `/clientes/{id}`      | Exclui um cliente (bloqueado se tiver entregas)              |
| GET    | `/motoristas`         | Lista os motoristas                                          |
| GET    | `/motoristas/{id}`    | Busca um motorista pelo id                                   |
| POST   | `/motoristas`         | Cadastra um motorista                                        |
| PUT    | `/motoristas/{id}`    | Atualiza um motorista                                        |
| DELETE | `/motoristas/{id}`    | Exclui um motorista (bloqueado se tiver entregas)            |
| GET    | `/entregas`           | Lista as entregas com cliente, motorista e datas             |
| POST   | `/entregas`           | Cadastra uma entrega (nasce Pendente)                        |
| PUT    | `/entregas/{id}`      | Despacha (`EmTransito` + motorista) ou conclui (`Entregue`)  |
| DELETE | `/entregas/{id}`      | Exclui uma entrega                                           |

## Estrutura do repositório

```
mini ERP logistico/
├── MiniErpLogistica/          # versão console (V1, SQLite)
└── MiniErpApi/                # versão atual
    ├── Program.cs             # configuração: banco, DI, JSON, CORS, rate limit, rotas
    ├── Models/                # entidades: Cliente, Motorista, Entrega, Sessao, StatusEntrega
    ├── Data/                  # AppDbContext (filtros por sessão), dados de exemplo, migrations
    ├── Dtos/                  # dados de entrada + validação
    ├── Endpoints/             # um arquivo por grupo de rotas
    ├── Services/              # sessão do visitante: middleware, gerenciador e limpeza
    ├── Migrations/
    └── wwwroot/index.html     # interface web (Rota ERP)
```

## Roadmap

- **Próximo — Notas fiscais com IA:** enviar a foto ou o PDF de uma nota, o Gemini preenche os campos, a API confere (CNPJ, chave de acesso, soma dos itens) e o usuário revisa antes de salvar.
- **Publicação:** deploy no Railway, testes automatizados e CI com GitHub Actions.
- **V2 — Veículo:** cadastro de frota, ligando motorista, veículo e entrega.
- **V3 — Histórico de status:** registrar cada mudança de status como um evento próprio.
- **Futuro:** autenticação de usuários e validação de entregas duplicadas.

## Autor

**Gustavo Delatore Telles**

---

Projeto de estudo em C#, .NET e desenvolvimento de APIs REST.
