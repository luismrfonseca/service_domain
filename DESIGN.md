# Desenho de Arquitetura: Service Domain (PHC CS Integration)

Este documento especifica a arquitetura e a estratégia de integração entre o ERP PHC CS (On-Premises) e o novo **Service Domain** concebido para expor WebAPIs para a app de Logística, app de Vendedores e o Portal Web.

---

## 1. Resumo do Entendimento

* **Objetivo:** Criar um serviço intermediário seguro e escalável que atua como repositório de dados local de alta velocidade para as aplicações satélite, desacoplando o ERP PHC de acessos massivos diretos.
* **Escala Alvo:** Mais de 100 utilizadores ativos concorrentes e mais de 1.000 encomendas diárias.
* **Stack Tecnológica:** C# / .NET para a WebAPI e Background Workers; Microsoft SQL Server para a base de dados local.
* **Direções de Sincronização:**
  * **Produtos e Lotes:** Unidirecional (ERP PHC $\rightarrow$ Service Domain).
  * **Clientes e Stocks/Localizações:** Bidirecional (ERP $\rightarrow$ Serviço e vice-versa).
  * **Encomendas de Cliente (Vendas):** Bidirecional em tempo real.
  * **Encomendas de Fornecedor (Compras):** Unidirecional (ERP $\rightarrow$ Serviço para exibição/preparação na Logística).
  * **Guias de Remessa / Receção (Logística):** Unidirecional de saída (Serviço $\rightarrow$ ERP PHC).

---

## 2. Pressupostos (Assumptions)

1. **Conectividade:** Existe uma ligação estável e de baixa latência (ex: rede local ou VPN dedicada) entre o servidor do Service Domain e o SQL Server do ERP PHC CS.
2. **Segurança:** A comunicação entre as Apps/Portal e a WebAPI é feita sob HTTPS, com autenticação baseada em tokens JWT.
3. **Padrão de Resiliência:** No caso de indisponibilidade ou lentidão temporária do SQL Server do PHC, o Service Domain armazena localmente os dados na fila e garante que as Apps continuam operacionais (modo offline/assíncrono).
4. **Integridade de Escrita:** O Service Domain calcula e gera corretamente todos os identificadores únicos (`u_stamp`), incrementa os contadores de documentos do PHC (ex: na tabela `boconf`) e preenche todos os campos obrigatórios respeitando as regras do PHC.
5. **Diferenciação de Documentos:** As Guias de Remessa e Receção são registadas no PHC nas tabelas `bo`/`bi` usando os tipos de documento e contadores respetivos geridos pela tabela `boconf` do ERP.

---

## 3. Decision Log (Registo de Decisões)

### 3.1. Método de Integração com o ERP PHC
* **Decisão:** Acesso direto à base de dados SQL Server do PHC.
* **Justificação:** Reutiliza o licenciamento existente, permite alta velocidade na leitura de dados de stocks/produtos sem sobrecarregar a camada aplicacional do PHC e aproveita o conhecimento interno em SQL.

### 3.2. Direção de Sincronização do Master Data
* **Decisão:** Mista/Bidirecional para Clientes e Stocks/Localizações; Unidirecional para Produtos/Lotes.
* **Justificação:** Necessidade operacional de os Vendedores criarem/editarem clientes em mobilidade e da Logística efetuar transferências físicas e acertos de stocks de imediato nas apps.

### 3.3. Frequência de Sincronização
* **Decisão:** Mista (Encomendas e Stock em tempo real/quase tempo real; Produtos, Clientes e Lotes periódicos a cada 15-30 minutos).
* **Justificação:** Garante que a informação crítica de stock e encomendas está sempre atualizada para evitar roturas de stock, enquanto minimiza o tráfego de dados de mestre estáticos.

### 3.4. Padrão de Arquitetura de Sincronização
* **Decisão:** Transactional Outbox com Background Workers assíncronos.
* **Justificação:** Evita locks longos no SQL Server do ERP PHC e garante resiliência total às apps satélite caso o ERP fique indisponível ou lento.

### 3.5. Estrutura de Documentos para Logística e Vendas
* **Decisão:** Modelo Unificado de Documentos (Mesma tabela `Encomenda` diferenciada por um campo `Tipo`).
* **Justificação:** Alinha-se diretamente com o modelo de dados do PHC (onde vendas e compras residem na `bo` e `bi`) e reduz a duplicação de entidades e lógica de mapeamento nos Workers.

---

## 4. Desenho Técnico Detalhado

### 4.1. Esquema de Base de Dados (Service Domain)

#### Enumeração `DocumentoTipo` (C#)
```csharp
public enum DocumentoTipo
{
    EncomendaCliente = 1,    // Sales Order (Vendas)
    EncomendaFornecedor = 2, // Purchase Order (Compras)
    GuiaRemessa = 3,         // Picking/Expedição (Saída)
    GuiaRececao = 4          // Receção de Compras (Entrada)
}
```

#### Tabelas de Documentos (`Encomendas` e `EncomendaLinhas`)
A tabela `Encomendas` local suportará agora chaves estrangeiras reflexivas para associar as guias aos documentos de origem (`ParentId`).

```sql
-- Alterações na tabela de Encomendas
ALTER TABLE Encomendas ADD
    Tipo INT NOT NULL DEFAULT 1,            -- Mapeia para DocumentoTipo
    ParentId UNIQUEIDENTIFIER NULL,        -- Associa guia ao documento pai
    CONSTRAINT FK_Encomendas_Parent FOREIGN KEY (ParentId) REFERENCES Encomendas(Id);

-- Alterações na tabela de EncomendaLinhas
ALTER TABLE EncomendaLinhas ADD
    ParentLineId UNIQUEIDENTIFIER NULL,    -- Associa linha da guia à linha da encomenda
    Localizacao NVARCHAR(50) NULL,         -- Local físico do stock movimentado
    CONSTRAINT FK_EncomendaLinhas_Parent FOREIGN KEY (ParentLineId) REFERENCES EncomendaLinhas(Id);
```

### 4.2. Fluxo da API de Logística (`LogisticaController`)

1. **`GET /api/logistica/picking/pendentes`**:
   * Retorna registos onde `Tipo = EncomendaCliente` e `Status = Sincronizado` (prontas para preparar).
2. **`POST /api/logistica/picking`**:
   * Recebe a confirmação de picking. Cria uma nova `Encomenda` local com `Tipo = GuiaRemessa` e `ParentId = encomendaId`. Insere o evento na `SyncOutbox` e atualiza a encomenda original para status "Preparada".
3. **`GET /api/logistica/rececao/pendentes`**:
   * Retorna registos onde `Tipo = EncomendaFornecedor` (encomendas de compra de fornecedores pendentes).
4. **`POST /api/logistica/rececao`**:
   * Recebe a receção de mercadoria. Cria uma nova `Encomenda` local com `Tipo = GuiaRececao` e `ParentId = encomendaId`. Incrementa o stock local (`Stocks`) nas localizações informadas e enfileira o evento na `SyncOutbox`.

### 4.3. Fluxo de Sincronização do Worker

* **OutboxWorker (Escrita no PHC):**
  * Para eventos `GuiaRemessa` ou `GuiaRececao`, o Worker lê o payload do JSON e insere diretamente no SQL Server do PHC nas tabelas `bo` e `bi`.
  * Utiliza as chaves nativas do PHC (ex: `bi.o_bistamp` apontando para o stamp da linha da encomenda de origem) para atualizar as linhas originais e dar baixa no ERP.
* **InboxWorker (Ingestão de Compras do PHC):**
  * O `InboxWorker` passa a monitorizar eventos do tipo `EncomendaFornecedor` no PHC. Ao ler uma nova encomenda de compra no PHC, insere-a na BD local com `Tipo = EncomendaFornecedor`.
