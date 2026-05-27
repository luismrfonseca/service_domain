-- Script DDL para criar a tabela de fila de sincronização no SQL Server do ERP PHC CS.
-- Esta tabela deve ser criada na base de dados de produção do PHC.
-- Triggers ou Regras de Negócio do PHC devem fazer INSERT nesta tabela sempre que existirem alterações.

CREATE TABLE u_phc_sync_queue (
    id INT IDENTITY(1,1) PRIMARY KEY,
    entity_type VARCHAR(50) NOT NULL,          -- 'Produto', 'Lote', 'Stock', 'Localizacao', 'Cliente', 'Encomenda'
    phc_stamp VARCHAR(25) NOT NULL,            -- O u_stamp do registo alterado (ex: st.ststamp, bo.bostamp)
    operation_type VARCHAR(10) NOT NULL,       -- 'INSERT', 'UPDATE', 'DELETE'
    status VARCHAR(20) NOT NULL DEFAULT 'Pendente', -- 'Pendente', 'Processado', 'Erro'
    error_message VARCHAR(MAX) NULL,
    created_at DATETIME NOT NULL DEFAULT GETDATE(),
    processed_at DATETIME NULL
);

-- Criar índices para acelerar a pesquisa pelo Worker de sincronização
CREATE INDEX IX_u_phc_sync_queue_Status_CreatedAt ON u_phc_sync_queue (status, created_at);
CREATE INDEX IX_u_phc_sync_queue_PhcStamp ON u_phc_sync_queue (phc_stamp);

GO

-- Exemplo conceitual de gatilho SQL (Trigger) no PHC para a tabela de clientes (cl):
/*
CREATE TRIGGER tr_cl_sync_to_service_domain
ON cl
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Inserir na fila de sincronização
    INSERT INTO u_phc_sync_queue (entity_type, phc_stamp, operation_type, status)
    SELECT 
        'Cliente', 
        i.clstamp, 
        CASE 
            WHEN EXISTS(SELECT 1 FROM deleted) THEN 'UPDATE' 
            ELSE 'INSERT' 
        END,
        'Pendente'
    FROM inserted i;
END;
*/
