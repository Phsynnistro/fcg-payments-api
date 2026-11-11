# FCG — Payments API

Microsserviço de **pagamentos** da FIAP Cloud Games. Consome `OrderPlacedEvent`, processa (simula) a cobrança e publica `PaymentProcessedEvent` com `Approved` ou `Rejected`. A simulação é **determinística**: compras acima de `PaymentSimulation__MaxApprovedAmount` (padrão R$ 300,00) são recusadas — o fluxo de rejeição pode ser demonstrado sob demanda.

> Parte da arquitetura de microsserviços da FIAP Cloud Games. Visão geral, diagramas, decisões (ADRs) e matriz de
> requisitos no repositório [fcg-orchestration](https://github.com/Phsynnistro/fcg-orchestration).

**Stack:** .NET 10 · ASP.NET Core Minimal APIs · EF Core 10 + PostgreSQL 17 · MassTransit 8.5 + RabbitMQ (Outbox/Inbox) · OpenTelemetry · xUnit

## Responsabilidades

- Consumir `OrderPlacedEvent` e cobrar via `IPaymentGateway` (hoje simulado).
- Persistir o pagamento e publicar `PaymentProcessedEvent` na mesma transação (consumer outbox).
- Garantir que um pedido **nunca seja cobrado duas vezes**.
- Permitir consulta do pagamento de um pedido.

## Endpoints

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| `GET` | `/api/payments/orders/{orderId}` | Dono ou Admin | Pagamento do pedido |

Todos os serviços expõem `/health/live` (liveness), `/health/ready` (readiness) e `/health` (diagnóstico em JSON).
Erros seguem **Problem Details (RFC 9457)** com `traceId`.

Documentação interativa: http://localhost:5103/scalar (com `dotnet run`) ou http://localhost:5003/scalar (compose).

## Eventos

| Direção | Evento | Exchange | Fila deste serviço | Campos principais |
|---|---|---|---|---|
| Consome | `OrderPlacedEvent` | `fcg.order-placed` | `payments.order-placed` | `OrderId`, `UserId`, `GameId`, `Price`, … |
| Publica | `PaymentProcessedEvent` | `fcg.payment-processed` | — | `OrderId`, `UserId`, `GameId`, `Price`, `Status`, `Reason`, `UserEmail`, `GameTitle`, `ProcessedAt` |

Contratos em `src/Fcg.Payments.Api/Contracts/Events`, namespace **`Fcg.Contracts.Events`** — idêntico em todos os
repositórios (o MassTransit identifica a mensagem por namespace + nome). Altere-os apenas de forma **aditiva**;
o `ContractTests` falha se a identidade do contrato mudar.

## Variáveis de ambiente

Configuração .NET via variáveis de ambiente: `__` (dois underscores) equivale a `:` — `RabbitMq__Host` → `RabbitMq:Host`.

| Variável | Exemplo | Origem no K8s | Descrição |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | ConfigMap | Ambiente do ASP.NET Core. |
| `RabbitMq__Host` | `rabbitmq` | ConfigMap | Host do broker (nome do Service no cluster). |
| `RabbitMq__Port` | `5672` | ConfigMap | Porta AMQP. |
| `RabbitMq__VirtualHost` | `/` | ConfigMap | Virtual host do RabbitMQ. |
| `RabbitMq__Username` | `fcg` | **Secret** | Usuário do broker. |
| `RabbitMq__Password` | `••••` | **Secret** | Senha do broker. |
| `ConnectionStrings__Database` | `Host=payments-db;Port=5432;Database=fcg_payments;Username=fcg_payments;Password=••••` | **Secret** | Connection string do PostgreSQL exclusivo do serviço. |
| `ApiDocs__Enabled` | `true` | ConfigMap | Expõe `/openapi/v1.json` e a UI Scalar em `/scalar`. |
| `Jwt__Issuer` | `fcg-users-api` | ConfigMap | Emissor esperado no token. |
| `Jwt__Audience` | `fcg` | ConfigMap | Audiência esperada no token. |
| `Jwt__MetadataAddress` | `http://users-api:80/.well-known/openid-configuration` | ConfigMap | Discovery do UsersAPI (nome do Service); a chave pública (JWKS) é baixada com retry/circuit breaker e cacheada. |
| `Jwt__RequireHttpsMetadata` | `false` | ConfigMap | `false` só dentro do cluster/compose. |
| `Messaging__Queues__OrderPlaced` | `payments.order-placed` | ConfigMap | Fila deste serviço para `OrderPlacedEvent`. |
| `Messaging__PrefetchCount` | `16` | ConfigMap | Mensagens pré-buscadas por consumidor. |
| `Messaging__ConcurrentMessageLimit` | `8` | ConfigMap | Processamento concorrente por réplica (bulkhead). |
| `PaymentSimulation__MaxApprovedAmount` | `300.00` | ConfigMap | Acima deste valor o pagamento é recusado. |
| `PaymentSimulation__ProcessingDelayMs` | `1500` | ConfigMap | Latência artificial (torna o estado `Pending` visível na demo e no trace). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://jaeger:4317` | ConfigMap | Coletor OTLP (Jaeger). Ausente ⇒ tracing não é exportado. |

Valores sensíveis (**Secret**) nunca entram no `appsettings.json`. O `appsettings.Development.json` contém valores
**apenas de desenvolvimento local**.

## Executar localmente

```bash
# 1) infraestrutura (a partir do repositório de orquestração)
cd ../fcg-orchestration && docker compose up -d rabbitmq jaeger payments-db users-api

# 2) o serviço
cd ../fcg-payments-api
dotnet run --project src/Fcg.Payments.Api        # http://localhost:5103
```

> A validação de JWT busca a chave pública no UsersAPI (`Jwt:MetadataAddress`), por isso ele sobe junto.

## Testes

```bash
dotnet test
```

Testes de domínio (regras e invariantes) e de **contrato** (identidade dos eventos).

## Docker

```bash
docker build -t fcg/payments-api:1.0.0 .            # imagem final: runtime chiseled, não-root, porta 8080
docker build --target test .                    # (opcional) roda os testes dentro do build
```

Dockerfile **multi-stage**: `build` (SDK) → `test` (opcional) → `final` (`aspnet:10.0-noble-chiseled`).
O CI analisa a imagem com **Trivy** e falha em vulnerabilidade CRITICAL/HIGH corrigível.

## Kubernetes

| Arquivo em [`k8s/`](k8s) | Conteúdo |
|---|---|
| `00-namespace.yaml` | Namespace `fcg` (permite aplicar este repositório isoladamente) |
| `01-configmap.yaml` | ConfigMap: filas, URLs de serviços, limites |
| `02-secrets.yaml` | Secrets: credenciais, connection string e senha do banco |
| `03-database.yaml` | PostgreSQL: PersistentVolumeClaim + Deployment (`Recreate`) + Service |
| `04-deployment.yaml` | Deployment da API: probes, recursos, securityContext, anotações |
| `05-service.yaml` | Service ClusterIP `payments-api:80` |
| `06-hpa.yaml` | HorizontalPodAutoscaler: 2–5 réplicas por CPU |

```bash
# pré-requisito: RabbitMQ e Jaeger do repositório fcg-orchestration (ou o ambiente completo dele)
cd k8s && kubectl apply -f .
kubectl -n fcg get pods -l app.kubernetes.io/name=payments-api
```

Este `/k8s` é a **fonte da verdade** do serviço. O repositório de orquestração mantém uma cópia sincronizada
(`scripts/k8s-sync-manifests.sh`) para subir o ambiente inteiro com um único `kubectl apply -f .` (ADR-0013).

## Estrutura

```
src/Fcg.Payments.Api/
├── Contracts/Events/                OrderPlacedEvent, PaymentProcessedEvent
├── Domain/Payments/                 Payment, PaymentStatus (domínio)
├── Application/Abstractions/        IPaymentGateway (porta)
├── Application/Payments/            ProcessOrderPaymentHandler, PaymentQueries
├── Infrastructure/Gateway/          SimulatedPaymentGateway (adapter)
├── Infrastructure/Persistence/      PaymentsDbContext, mapeamento
├── Infrastructure/Observability/    OpenTelemetry
└── Presentation/                    consumer OrderPlacedConsumer, endpoint, health
tests/Fcg.Payments.UnitTests/
k8s/                                 manifestos Kubernetes (00 a 06)
Dockerfile · Fcg.Payments.sln · Directory.Build.props · Directory.Packages.props (versões centralizadas) · global.json
```

## Decisões de design

- **Idempotência de cobrança em 3 camadas**: inbox do MassTransit (MessageId), checagem de pagamento existente (republica o resultado original **sem recobrar**) e índice único em `OrderId`.
- O handler roda **dentro da transação do outbox** (`RepeatableRead`) aberta pelo MassTransit: `Payment` + `PaymentProcessedEvent` + registro no inbox são atômicos.
- **Porta `IPaymentGateway`**: trocar a simulação por um provedor real (Stripe, Pagar.me) não toca no caso de uso.
- **Enum de domínio separado do enum do contrato**: o modelo interno pode evoluir (ex.: `Refunded`) sem quebrar consumidores.
- **Competing consumers** + **HPA**: as réplicas leem a MESMA fila `payments.order-placed` e o broker distribui a carga.
- **Resiliência**: retry exponencial e kill switch no consumo (ADR-0012).
