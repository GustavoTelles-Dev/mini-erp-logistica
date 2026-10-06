# Mini ERP de Logística — Rota ERP

Sistema de gestão logística (clientes, motoristas, entregas e notas fiscais) feito em **C# / .NET 8**, com banco **PostgreSQL (Supabase)**, **leitura de notas fiscais por IA (Google Gemini)** e uma interface web própria, o **Rota ERP**.

> **Demonstração online:** _link disponível após o deploy no Railway_
> Cada visitante ganha uma cópia própria dos dados: pode criar, editar e excluir à vontade, e nada afeta outra pessoa.

<!-- GIF da demonstração: docs/demo.gif -->

---

## Teste em 1 minuto

1. **Abra a demonstração.** O painel já vem com uma semana de entregas de exemplo.
2. **Despache uma entrega:** em *Entregas*, abra uma entrega **Pendente**, escolha o motorista e clique em *Despachar*. Depois clique em *Confirmar entrega* e veja a linha do tempo e o painel se atualizarem.
3. **Cadastre uma nota fiscal com IA:** em *Notas fiscais*, clique em *Nova nota* e depois em **"Testar com a nota de exemplo"** (uma nota fictícia). A IA preenche os campos, o sistema confere CNPJ, chave de acesso e totais, e você revisa antes de salvar.
4. **Teste as validações:** troque um dígito do CNPJ da nota ou tente salvar um cliente sem nome. As mensagens aparecem no campo certo.
5. **Quer recomeçar?** O botão **Restaurar dados** volta tudo ao estado inicial.

A API também pode ser explorada pelo **Swagger** (`/swagger`): chame `GET /sessao` primeiro e os outros endpoints passam a funcionar.

---

## O que o sistema faz

O domínio gira em torno de **entregas**. Cada entrega pertence a um cliente e, ao ser despachada, ganha um motorista. O status segue um ciclo fixo, sem pular etapas:

```
Pendente  ──(despacho, exige motorista)──▶  Em trânsito  ──(confirmação)──▶  Entregue
```

Cada etapa registra a data em que aconteceu; essas datas alimentam os gráficos do painel e o histórico de cada entrega.

- **Cliente** — solicita entregas; cliente inativo não recebe entregas novas.
- **Motorista** — leva as entregas.
- **Entrega** — pertence a um cliente e recebe um motorista no despacho.
- **Nota fiscal** — a nota da mercadoria que a entrega leva, com seus itens.

Cliente ou motorista com entregas, e entrega com nota, não podem ser excluídos (a API avisa e o banco também impede).

## Notas fiscais com IA: a IA lê, o código confere

1. O usuário envia a foto ou o PDF da nota.
2. A API manda o arquivo ao **Gemini** exigindo um formato de resposta fixo (*structured output*). A IA também aponta os campos que leu com dúvida.
3. Antes de mostrar o resultado, a leitura passa por **regras fiscais escritas em C#** (`ConferenciaNota`):
   - dígitos verificadores do **CNPJ** (inclusive o novo CNPJ alfanumérico) e do **CPF**;
   - **chave de acesso** com 44 dígitos e dígito verificador (módulo 11);
   - cruzamento da chave com CNPJ, série, número e mês/ano de emissão;
   - quantidade × valor unitário de cada item e soma dos itens × total da nota.
4. A tela mostra os campos preenchidos (marcados com **IA**), destaca os incertos e lista os avisos. Tudo continua editável.
5. Só ao confirmar a nota é salva, e a API **valida tudo de novo** antes de gravar: o front ajuda, quem decide é o servidor.

## Segurança e robustez

O sistema é público, então foi pensado para aguentar uso errado, abuso e falhas, sempre respondendo com uma mensagem clara (nunca uma tela quebrada):

| Situação | Como o sistema responde |
|---|---|
| Um visitante tentar ver dados de outro | Impossível: toda consulta é filtrada pela sessão do visitante (filtro global do EF Core) |
| Chave do Gemini e senha do banco | Ficam só em *user-secrets* / variáveis de ambiente; nunca no código, no GitHub ou no navegador |
| Script malicioso (XSS) | Todo dado é escapado na tela, e a **CSP** com hash só deixa rodar os scripts do próprio sistema |
| Duas abas alterando a mesma entrega (*race condition*) | Concorrência otimista (`xmin` do Postgres): a segunda recebe "registro alterado em outra aba" |
| Vários envios de nota ao mesmo tempo | A sessão é travada no banco (`SELECT … FOR UPDATE`): o limite de notas nunca é ultrapassado |
| Arquivo grande demais ou falso | Limite de 5 MB e checagem da **assinatura real dos bytes** (um `.exe` renomeado para `.jpg` é recusado) |
| Cota do Gemini esgotada ou IA lenta | Teto diário de leituras, fila de 3 leituras simultâneas, tempo limite de 40 s e **modelo reserva**; se nada der, o usuário é avisado e preenche à mão |
| Excesso de requisições | *Rate limiting* por IP (geral, criação de sessão, leitura por IA e envio de notas) |
| Banco cheio por abuso | Limites por sessão (cadastros, notas, espaço de arquivos) e sessões apagadas após 6 h sem uso |
| Banco fora do ar | A API responde 503 com mensagem amigável e, ao iniciar, tenta conectar de novo antes de desistir |
| Internet do usuário caiu | O front mostra um aviso de "sem conexão" e recarrega os dados quando ela volta |
| Erro inesperado | Tratador central: o usuário vê uma mensagem simples, os detalhes ficam só no log do servidor |

Também: cabeçalhos HTTP de segurança (HSTS, `nosniff`, proteção contra *clickjacking*), cookie `HttpOnly` + `SameSite=Strict`, CORS liberado só em desenvolvimento e validação de todos os dados de entrada no servidor.

## Tecnologias

- **C# / .NET 8** (LTS) com **ASP.NET Core Minimal API**
- **Entity Framework Core 8** + **Npgsql** (PostgreSQL no **Supabase**)
- **Google Gemini** (SDK oficial `Google.GenAI`) com saída estruturada
- **Swagger** para documentação e testes da API
- **HTML, CSS e JavaScript puro** na interface (sem framework)

## Conceitos que o projeto exercita

- CRUD **assíncrono** de ponta a ponta, **injeção de dependência** e endpoints organizados por grupo
- **DTOs com validação** e erros no padrão **Problem Details** (RFC 9457)
- Regra de **transição de status** com `enum` e integridade referencial (API + banco)
- **Multi-tenant** com filtros globais, *middleware* e serviço em segundo plano (`BackgroundService`)
- **IA com saída estruturada + validação determinística**, atrás de uma interface (`ILeitorDeNota`) para trocar de provedor sem mexer no resto
- Upload `multipart/form-data`, **concorrência otimista**, transações com trava de linha e *rate limiting*
- Migrations aplicadas automaticamente ao iniciar

## Como executar no seu computador

**Pré-requisitos:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e um projeto no [Supabase](https://supabase.com) (o plano gratuito serve).

1. No Supabase, abra **Connect** → tipo **.NET** → método **Session pooler** e copie a connection string (com a sua senha).
2. Guarde a string nos *user-secrets* (fica fora da pasta do projeto e nunca vai para o Git):

```bash
cd MiniErpApi
dotnet user-secrets set "ConnectionStrings:Supabase" "Host=...;Database=postgres;Username=...;Password=...;..."
```

3. *(Opcional)* Para a leitura por IA, gere uma chave no [Google AI Studio](https://aistudio.google.com/apikey). Sem ela o sistema funciona e as notas são digitadas à mão:

```bash
dotnet user-secrets set "Gemini:ChaveApi" "SUA_CHAVE"
```

4. Rode. Na primeira execução as tabelas são criadas sozinhas:

```bash
dotnet run
```

- Interface web: `http://localhost:5185`
- Swagger: `http://localhost:5185/swagger`
- O arquivo `MiniErpApi/MiniErpApi.http` tem todas as chamadas prontas para testar no VS Code.

## Endpoints da API

Os endpoints de dados exigem a sessão (header `X-Sessao` ou cookie criado por `GET /sessao`).

| Método | Rota | Descrição |
|---|---|---|
| GET | `/sessao` | Devolve a sessão do visitante (ou cria uma com dados de exemplo) |
| POST | `/sessao/reiniciar` | Volta a sessão aos dados de exemplo |
| GET · POST | `/clientes` | Lista / cadastra clientes |
| PUT · DELETE | `/clientes/{id}` | Atualiza / exclui (bloqueado se tiver entregas) |
| GET · POST | `/motoristas` | Lista / cadastra motoristas |
| GET · PUT · DELETE | `/motoristas/{id}` | Busca / atualiza / exclui (bloqueado se tiver entregas) |
| GET · POST | `/entregas` | Lista (com cliente, motorista e datas) / cadastra (nasce Pendente) |
| PUT | `/entregas/{id}` | Despacha (`EmTransito` + motorista) ou conclui (`Entregue`) |
| DELETE | `/entregas/{id}` | Exclui (bloqueado se tiver nota fiscal) |
| GET | `/notas` | Lista as notas fiscais |
| GET · DELETE | `/notas/{id}` | Detalhe com itens / exclui a nota e os itens |
| GET | `/notas/{id}/arquivo` | Arquivo original (imagem ou PDF) |
| POST | `/notas/interpretar` | Lê a nota com IA e confere (não salva) |
| POST | `/notas` | Salva a nota conferida pelo usuário |
| GET | `/saude` | Verificação de saúde (API + banco) |

## Estrutura do repositório

```
mini ERP logistico/
├── MiniErpLogistica/        # V1: versão console (SQLite), onde a lógica nasceu
└── MiniErpApi/              # versão atual
    ├── Program.cs           # configuração: banco, DI, limites, segurança, rotas
    ├── Models/              # entidades
    ├── Data/                # AppDbContext (filtros por sessão) e dados de exemplo
    ├── Dtos/                # dados de entrada + validação
    ├── Endpoints/           # um arquivo por grupo de rotas
    ├── Services/            # sessão, IA, conferência fiscal, erros, segurança e limites
    ├── Migrations/
    └── wwwroot/             # interface web (Rota ERP) + nota fiscal fictícia de exemplo
```

## A história do projeto

Comecei como uma aplicação de **console**, para firmar a lógica de negócio e a persistência. Depois evoluí para uma **API REST**, ganhei uma interface web, troquei o SQLite pelo PostgreSQL e adicionei a leitura de notas com IA. A ideia sempre foi construir uma base sólida e ir trocando as "portas de entrada" sem reescrever o núcleo.

**Próximos passos:** testes automatizados e CI (GitHub Actions), cadastro de frota (veículo ligado a motorista e entrega) e histórico de status como eventos.

## Autor

**Gustavo Delatore Telles** — projeto de estudo e portfólio em C#, .NET e APIs REST.
