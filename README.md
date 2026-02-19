## POC - Order Management

Projeto POC: API em .NET (OrderService.Api), Worker (OrderService.Worker), Frontend React (Vite + Tailwind), Postgres e PgAdmin orquestrados com Docker Compose.

### Principais componentes
- `backend/OrderService.Api` — API REST (.NET) com endpoints: `POST /orders`, `GET /orders`, `GET /orders/{id}`.
- `backend/OrderService.Worker` — Worker que processa a Outbox ou mensagens do Azure Service Bus e atualiza status.
- `frontend` — Single-page app (Vite + React + Tailwind).
- `docker-compose.yml` — orquestra todos os serviços.

### Quickstart (Docker - recomendado)
1. Copie o arquivo de variáveis e edite conforme seu ambiente:

```bash
cp .env.example .env
# editar .env conforme necessário (SERVICE_BUS_CONNECTIONSTRING, VITE_API_URL, etc.)
```

2. Subir a stack:

```bash
docker compose up --build -d
```

3. Endpoints úteis
- API: http://localhost:5000
- Frontend: http://localhost:5173
- API health: `GET /health` (simple) e `GET /hc` (detailed)

### Migrations (explicação)
- O código chama `Database.Migrate()` no startup de `api` e `worker` para aplicar migrations automaticamente.
- Para ter o histórico de migrations no repositório / CI, gere as migrations localmente e commit:

Local (com .NET SDK instalado):
```bash
cd backend/OrderService.Api
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet tool restore || true
dotnet ef migrations add InitialCreate
dotnet ef database update --connection "$POSTGRES_CONN"
```

Usando imagem SDK (sem instalar SDK local):
```bash
docker run --rm -v "$PWD/backend/OrderService.Api":/src -w /src mcr.microsoft.com/dotnet/sdk:7.0 \
	bash -lc "dotnet tool install --global dotnet-ef --version 7.*; export PATH=\"\$PATH:\$HOME/.dotnet/tools\"; dotnet ef migrations add InitialCreate; dotnet ef database update --connection \"${POSTGRES_CONN}\""
```

Observação: para simplificar o POC, uma baseline (`20260219000000_InitialCreate`) foi adicionada ao repositório; se preferir gerar migrations oficiais, remova a baseline e gere com `dotnet ef`.

### Outbox + Worker
- Ao criar um `Order` a API grava um registro em `OutboxMessages` e tenta (se configurado) publicar no Azure Service Bus.
- O `Worker` processa a Outbox: atualiza `Order.Status` de `Pending` → `Processing`, espera 5s e atualiza para `Finalized`, marcando a Outbox como processada.
- O consumidor é idempotente: verifica o status antes de processar.

### Variáveis de ambiente (exemplo)
```ini
# Database
POSTGRES_DB=ordersdb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_CONN=Host=postgres;Database=ordersdb;Username=postgres;Password=postgres

# API
ASPNETCORE_URLS=http://+:5000

# Frontend
VITE_API_URL=http://localhost:5000

# Azure Service Bus (opcional)
SERVICE_BUS_CONNECTIONSTRING=
SERVICE_BUS_QUEUE=orders-queue
```

### Healthchecks
- API expõe `/health` (simple) e `/hc` (detailed). O Compose possui healthcheck para `postgres` e o `api` tem healthcheck configurado que consulta `/hc`.

### Debug / Logs
- Logs dos containers: `docker compose logs -f api`, `docker compose logs -f worker`, `docker compose logs -f frontend`.
- Consultas diretas ao Postgres: `docker compose exec -T postgres psql -U postgres -d ordersdb`.

### Checklist de entrega
- Código-fonte completo (esta repo)
- README.md com instruções (este arquivo)
- `.env.example` incluído no repositório (ver `.env.example`)
- Diagrama de arquitetura: opcional — adicione `docs/architecture.png` ou `docs/architecture.svg` para complementar

### Problemas comuns
- Se o frontend não refletir mudanças, rebuild do frontend sem cache: `docker compose build --no-cache frontend && docker compose up -d frontend`.
- Se `dotnet ef` não encontrar a tool, exporte `PATH` para `$HOME/.dotnet/tools` ou use a imagem SDK.

### Itens pendentes
- Persistir histórico de status (audit table)
- Adicionar retries/backoff no worker e métricas/tracing
- Adicionar testes automatizados e pipeline CI

