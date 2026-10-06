# Mini ERP de Logística

Sistema de gestão logística para controle de clientes, motoristas, entregas e notas fiscais, escrito em C# com .NET 8, com banco PostgreSQL no Supabase, leitura de notas fiscais por IA (Google Gemini) e uma interface web própria, o **Rota ERP**.

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
- **Nota fiscal** — a nota da mercadoria que a entrega leva, com seus itens. Uma entrega pode ter várias notas.

## Notas fiscais com IA

A nota fiscal é cadastrada sem digitação, mas nunca sem conferência humana:

1. O usuário envia a foto ou o PDF da nota (ou usa a **nota de exemplo fictícia** que vem no sistema).
2. A API manda o arquivo para o **Gemini** com um formato de resposta fixo (*structured output*): chave de acesso, número, série, data, emitente, destinatário, valor total e itens. A IA também aponta os campos que leu com dúvida.
3. **A IA lê; o código confere.** Antes de devolver, a API passa a leitura por regras fiscais escritas em C# (`ConferenciaNota`):
   - dígitos verificadores do CNPJ (inclusive o CNPJ alfanumérico) e do CPF;
   - chave de acesso com 44 dígitos e dígito verificador (módulo 11);
   - cruzamento da chave com o CNPJ, a série, o número e o mês/ano de emissão;
   - quantidade × valor unitário de cada item e soma dos itens × total da nota.
4. A tela mostra os campos preenchidos (marcados com **IA**), destaca os incertos e lista os avisos da conferência. Tudo continua editável, e a conferência roda de novo a cada alteração.
5. Só ao confirmar a nota é salva. A API valida tudo outra vez antes de gravar — o front ajuda, mas quem decide é o servidor.

Detalhes de robustez: o arquivo é validado pela assinatura real dos bytes (não só pela extensão), a leitura tem limite de uso por IP e, se o modelo principal estiver sobrecarregado, a API tenta automaticamente um modelo reserva mais leve.

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
- **Google Gemini** (SDK oficial `Google.GenAI`) — leitura das notas fiscais
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
- Limite de requisições (**rate limiting**) e configuração segura (connection string e chave da IA fora do código)
- **IA com saída estruturada** + validação determinística, interface (`ILeitorDeNota`) para trocar de provedor sem mexer no resto e modelo reserva em caso de falha
- Upload `multipart/form-data` com validação de tipo, tamanho e assinatura do arquivo
- Migrations aplicadas automaticamente ao iniciar a aplicação

## Como executar

**Pré-requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e um projeto no [Supabase](https://supabase.com) (o plano gratuito serve).

1. No Supabase, abra **Connect**, escolha o tipo **.NET** e o método **Session pooler**, e copie a connection string (com a sua senha).
2. Guarde a string nos *user-secrets* — ela fica fora da pasta do projeto e nunca vai para o Git:

```bash
cd MiniErpApi
dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Database=postgres;Username=...;Password=...;..."
```

3. (Opcional) Para a leitura de notas por IA, gere uma chave no [Google AI Studio](https://aistudio.google.com/apikey) e guarde do mesmo jeito. Sem ela, o sistema funciona e as notas são preenchidas à mão:

```bash
dotnet user-secrets set "Gemini:ChaveApi" "SUA_CHAVE"
```

4. Rode a API. Na primeira execução as tabelas são criadas sozinhas (migrations automáticas):

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
| DELETE | `/entregas/{id}`      | Exclui uma entrega (bloqueado se tiver nota fiscal)          |
| GET    | `/notas`              | Lista as notas fiscais (com a entrega e o cliente)           |
| GET    | `/notas/{id}`         | Detalhe da nota com os itens                                 |
| GET    | `/notas/{id}/arquivo` | Arquivo original da nota (imagem ou PDF)                     |
| POST   | `/notas/interpretar`  | Lê a nota com IA e confere (não salva)                       |
| POST   | `/notas`              | Salva a nota conferida pelo usuário                          |
| DELETE | `/notas/{id}`         | Exclui uma nota e seus itens                                 |

## Estrutura do repositório

```
mini ERP logistico/
├── MiniErpLogistica/          # versão console (V1, SQLite)
└── MiniErpApi/                # versão atual
    ├── Program.cs             # configuração: banco, DI, JSON, CORS, rate limit, rotas
    ├── Models/                # entidades: Cliente, Motorista, Entrega, NotaFiscal, ItemNota, Sessao
    ├── Data/                  # AppDbContext (filtros por sessão), dados de exemplo, migrations
    ├── Dtos/                  # dados de entrada + validação
    ├── Endpoints/             # um arquivo por grupo de rotas
    ├── Services/              # sessão do visitante, leitura com Gemini e conferência fiscal
    ├── Migrations/
    └── wwwroot/
        ├── index.html         # interface web (Rota ERP)
        └── exemplos/          # nota fiscal fictícia para testar a leitura por IA
```

## Roadmap

- **Próximo — Publicação:** deploy no Railway, testes automatizados e CI com GitHub Actions.
- **V2 — Veículo:** cadastro de frota, ligando motorista, veículo e entrega.
- **V3 — Histórico de status:** registrar cada mudança de status como um evento próprio.
- **Futuro:** autenticação de usuários e validação de entregas duplicadas.

## Autor

**Gustavo Delatore Telles**

---

Projeto de estudo em C#, .NET e desenvolvimento de APIs REST.
